using System;
using System.Text;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using SpaceEngineers.ObjectBuilders.ObjectBuilders;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Dhr.HEAmmo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]

    class BleedScriptBullet : MySessionComponentBase
    {
        public static BleedScriptBullet Instance;

        public static int BleedLength = 800;
        public static float BleedLengthMultiplier = 1.01f;
        public static int MainDamageDivisior = 30;


        Dictionary<long, List<BleedEffect>> existingEffectDic = new Dictionary<long, List<BleedEffect>>();
        List<BleedEffect> bleedingEntityList = new List<BleedEffect>();

        public override void LoadData()
        {
            Instance = this;

        }
        public override void BeforeStart()
        {
            // Get projectiles and add projectile hit lower function
            IMyProjectiles projectiles = MyAPIGateway.Projectiles;

            projectiles.AddOnHitInterceptor(10000, ProjectileBleedHit);
                    
        }

        protected override void UnloadData()
        {
            Instance = null;
        }

        public override void UpdateAfterSimulation()
        {
            // Run damage tick
            for (int i = 0; i < bleedingEntityList.Count; i++)
            {
                BleedEffect currentEffect = bleedingEntityList[i];

                // Check if effect should be remove
                if (currentEffect.IsEffectOver())
                {
                    // Get dictionary for bleed effect
                    List<BleedEffect> bleedList;

                    if (existingEffectDic.TryGetValue(currentEffect.ID.EntityId, out bleedList))
                    {
                        // Check if this is the last effect, if so remove from memory
                        if (bleedList.Count == 1)
                        {
                            existingEffectDic.Remove(currentEffect.ID.EntityId);
                        }
                        else
                        {
                            // Check for element and remove it from list
                            for (int j = 0; j < bleedList.Count; j++)
                            {
                                if (bleedList[j].GetHashCode() == currentEffect.GetHashCode())
                                {
                                    bleedList.RemoveAt(j);
                                    break;
                                }
                            }
                        }
                    }

                    // Terminate its place in memory
                    bleedingEntityList.RemoveAt(i);
                }
                else
                {
                    currentEffect.RunDamageTick();
                }
            }
        }

        void ProjectileBleedHit(ref MyProjectileInfo projectile, ref MyProjectileHitInfo hit)
        {

            // Check if hit entity is a character
            if (hit.HitEntity is IMyCharacter)
            {
                // Get character
                IMyCharacter hitCharacter = (IMyCharacter)hit.HitEntity;

                // Create mulpliers
                float damageDivided = hit.Damage / MainDamageDivisior;

                float movementMultiplier = 1 / (float)Math.Pow(BleedLengthMultiplier, hit.Damage);
                float originalMovementMultiplier = movementMultiplier;
                int ticksOfEffect = BleedLength * (int)Math.Pow(BleedLengthMultiplier, hit.Damage);

                List<BleedEffect> bleedList;

                // Check if an effect already exists, if so, edit that, else 
                if (existingEffectDic.TryGetValue(hitCharacter.EntityId, out bleedList))
                {

                    // Create new effect
                    BleedEffect newBleedEffect = new BleedEffect(damageDivided, movementMultiplier, originalMovementMultiplier, hitCharacter, ticksOfEffect);

                    // Add effect into system
                    bleedList.Add(newBleedEffect);
                    bleedingEntityList.Add(newBleedEffect);
                }
                else
                {
                    bleedList = new List<BleedEffect>();

                    // Add effect
                    BleedEffect newBleedEffect = new BleedEffect(damageDivided, movementMultiplier, originalMovementMultiplier, hitCharacter, ticksOfEffect);

                    // Add bleed effect to active effects list
                    bleedingEntityList.Add(newBleedEffect);
                    bleedList.Add(newBleedEffect);

                    // Create dictionary location
                    existingEffectDic.Add(hitCharacter.EntityId, bleedList);

                    hitCharacter.OnClose += RemoveEffect;
                }
            }
            else
            {
                return;
            }
        }

        void RemoveEffect(VRage.ModAPI.IMyEntity character)
        {
            existingEffectDic.Remove(character.EntityId);

            // Go through all objects
            for (int i = 0; i < bleedingEntityList.Count; i++)
            {
                // Check if object is the one being removed
                BleedEffect effect = bleedingEntityList[i];

                if (character.EntityId == effect.ID.EntityId)
                {
                    // Close it
                    bleedingEntityList.RemoveAt(i);
                    effect = null;
                    return;
                }
            }
        }

        /// <summary>
        /// Used for storing the info of bleed caused to an entity
        /// </summary>
        public class BleedEffect
        {
            // Internal variables
            private float damage;
            private float startDamage;
            private int ticks;
            private IMyCharacter entity;

            // Get for variables
            public float Damage { get { return damage; } }
            public IMyCharacter ID { get { return entity; } }



            /// <summary>
            /// Create a new bleed effect on character
            /// </summary>
            /// <param name="Damage">Damage dished out per tick</param>
            /// <param name="movementMultiplier">Speed multiplier caused from hit</param>
            /// <param name="character">Entity getting effected with bleed</param>
            public BleedEffect(float DPS, float movementMultiplier, float OGsmovementMultiplier, IMyCharacter character, int ticksOfEffect)
            {
                // Set internal info to item
                damage = DPS / 60;
                startDamage = damage;
                entity = character;
                ticks = ticksOfEffect;

                MyCharacterDefinition charDef = character.Definition as MyCharacterDefinition;
            }

            /// <summary>
            /// Check if the effect is ending
            /// </summary>
            /// <returns></returns>
            public bool IsEffectOver()
            {
                // Check if there are any damage ticks left
                if (ticks == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Run a bleed damage tick for the entity
            /// </summary>
            public void RunDamageTick()
            {
                // Do damage
                entity.DoDamage(damage + (1 / 120), new MyStringHash(), true);
                
                // Tack damage off for next tick
                damage -= startDamage / ticks;
                ticks -= 1;
            }
        }
    }
}
