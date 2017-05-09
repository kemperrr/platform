﻿
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Util;
using GTANetwork.Streamer;
using GTANetworkShared;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Sync
{
    internal partial class SyncPed
    {
        private bool CreateVehicle()
        {
            var PlayerChar = Game.Player.Character;
            if (_isInVehicle && MainVehicle != null && Character.IsInVehicle(MainVehicle) && PlayerChar.IsInVehicle(MainVehicle) && VehicleSeat == -1 && Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER, PlayerChar) == -1 && Util.Util.GetPedSeat(PlayerChar) == 0)
            {
                Character.Task.WarpOutOfVehicle(MainVehicle);
                PlayerChar.Task.WarpIntoVehicle(MainVehicle, GTA.VehicleSeat.Driver);
                Events.LastCarEnter = DateTime.Now;
                Script.Yield();
                return true;
            }

            var createVehicle = !_lastVehicle && _isInVehicle || _lastVehicle && _isInVehicle && (MainVehicle == null || !Character.IsInVehicle(MainVehicle) && PlayerChar.VehicleTryingToEnter != MainVehicle || VehicleSeat != Util.Util.GetPedSeat(Character) && PlayerChar.VehicleTryingToEnter != MainVehicle);

            if (!Debug && MainVehicle != null)
            {
                createVehicle = createVehicle || Main.NetEntityHandler.EntityToNet(MainVehicle.Handle) != VehicleNetHandle;
            }
            if (!createVehicle) return false;

            MainVehicle = new Vehicle(Main.NetEntityHandler.NetToEntity(VehicleNetHandle)?.Handle ?? 0);

            //if (MainVehicle == null || MainVehicle.Handle == 0)
            //{
            //    Character.Position = Position;
            //    return true;
            //}

            //if (PlayerChar.IsInVehicle(MainVehicle) && VehicleSeat == Util.Util.GetPedSeat(PlayerChar))
            //{
            //    if (DateTime.Now.Subtract(Events.LastCarEnter).TotalMilliseconds < 1000) return true;

            //    PlayerChar.Task.WarpOutOfVehicle(MainVehicle);
            //    NativeUI.BigMessageThread.MessageInstance.ShowMissionPassedMessage("~r~Car jacked!", 3000);
            //}

            if (MainVehicle != null && MainVehicle.Handle != 0)
            {
                //if (VehicleSeat == -1)
                //{
                //    //MainVehicle.Position = VehiclePosition;
                //}
                //else
                //{
                //    Character.PositionNoOffset = MainVehicle.Position;
                //}
                //Character.PositionNoOffset = MainVehicle.Position;

                MainVehicle.IsEngineRunning = true;
                MainVehicle.IsInvincible = true;
                if (EnteringVehicle)
                {
                    Character.Task.EnterVehicle(MainVehicle, (VehicleSeat)VehicleSeat, -1, 2f);
                    while (Character.IsSubtaskActive(ESubtask.ENTERING_VEHICLE_GENERAL) || Character.IsSubtaskActive(ESubtask.ENTERING_VEHICLE_ENTERING))
                    {
                        Script.Yield();
                        Script.Wait(2000);
                        Character.SetIntoVehicle(MainVehicle, (VehicleSeat)VehicleSeat);
                    }
                }
                else
                {
                    Character.SetIntoVehicle(MainVehicle, (VehicleSeat)VehicleSeat);
                }
                
            }
            _lastVehicle = true;
            _justEnteredVeh = true;
            _enterVehicleStarted = DateTime.Now;
            return true;
        }

        private void UpdateVehiclePosition()
        {
            if (MainVehicle == null || Character.CurrentVehicle == null) return;

            UpdateVehicleMountedWeapon();

            if (IsCustomAnimationPlaying) DisplayCustomAnimation();

            if (ExitingVehicle && !_lastExitingVehicle)
            {
                Character.Task.ClearAll();
                Character.Task.ClearSecondary();

                if (Speed < 1f)
                {
                    MainVehicle.Doors[(VehicleDoorIndex)VehicleSeat + 1].Open(true, true);
                    Character.Task.LeaveVehicle(MainVehicle, false);
                    Script.Wait(2000);
                    Character.PositionNoOffset = Position;
                }
                else
                {
                    Function.Call(Hash.TASK_LEAVE_VEHICLE, Character, MainVehicle, 4160);
                }
            }

            if (!ExitingVehicle && _lastExitingVehicle) DirtyWeapons = true;

            _lastExitingVehicle = ExitingVehicle;

            if (ExitingVehicle) return;

            if (GetResponsiblePed(MainVehicle).Handle == Character.Handle && Environment.TickCount - LastUpdateReceived < 10000)
            {
                UpdateVehicleMainData();
                if (DisplayVehicleDriveBy()) return;
            }
        }

        private void UpdateVehicleMainData()
        {
            UpdateVehicleInternalInfo();
            DisplayVehiclePosition();
        }

        private void UpdateVehicleInternalInfo()
        {
            if (MainVehicle.MemoryAddress == IntPtr.Zero) return;

            MainVehicle.EngineHealth = VehicleHealth;
            if (IsVehDead && !MainVehicle.IsDead)
            {
                MainVehicle.IsInvincible = false;
                MainVehicle.Explode();
            }

            else if (!IsVehDead && MainVehicle.IsDead)
            {
                MainVehicle.IsInvincible = true;
                if (MainVehicle.IsDead) MainVehicle.Repair();
            }

            //MainVehicle.PrimaryColor = (VehicleColor) VehiclePrimaryColor;
            //MainVehicle.SecondaryColor = (VehicleColor) VehicleSecondaryColor;
            
			//if (VehicleMods != null && _modSwitch % 50 == 0 &&
			//	Main.PlayerChar.IsInRangeOfEx(Position, 30f))
			//{
			//	var id = _modSwitch / 50;

			//	if (VehicleMods.ContainsKey(id) && VehicleMods[id] != MainVehicle.GetMod(id))
			//	{
			//		Function.Call(Hash.SET_VEHICLE_MOD_KIT, MainVehicle.Handle, 0);
			//		MainVehicle.SetMod(id, VehicleMods[id], false);
			//		Function.Call(Hash.RELEASE_PRELOAD_MODS, id);
			//	}
			//}
			//_modSwitch++;
            
			//if (_modSwitch >= 2500)
			//	_modSwitch = 0;
                
            Function.Call(Hash.USE_SIREN_AS_HORN, MainVehicle, Siren); // No difference?

            if (IsHornPressed && !_lastHorn)
            {
                _lastHorn = true;
                MainVehicle.SoundHorn(99999);
            }

            if (!IsHornPressed && _lastHorn)
            {
                _lastHorn = false;
                MainVehicle.SoundHorn(1);
            }

            if (IsInBurnout && !_lastBurnout)
            {
                Function.Call(Hash.SET_VEHICLE_BURNOUT, MainVehicle, true);
                Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Character, MainVehicle, 23, 120000); // 30 - burnout
            }

            if (!IsInBurnout && _lastBurnout)
            {
                Function.Call(Hash.SET_VEHICLE_BURNOUT, MainVehicle, false);
                Character.Task.ClearAll();
            }

            _lastBurnout = IsInBurnout;

            Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, MainVehicle, Speed > 0.2 && _lastSpeed > Speed);

            if (MainVehicle.SirenActive && !Siren)
            {
                MainVehicle.SirenActive = Siren;
            }
            else if (!MainVehicle.SirenActive && Siren)
            {
                MainVehicle.SirenActive = Siren;
            }

            MainVehicle.CurrentRPM = VehicleRPM;
            MainVehicle.SteeringAngle = SteeringScale.ToRadians();

            if (MainVehicle.ClassType == VehicleClass.Helicopters)
            {
                Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, MainVehicle.GetHashCode());
                Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, MainVehicle);
            }
        }

        private void VMultiVehiclePos()
        {
            var vecDif = Position - currentInterop.vecStart; // Différence entre les deux positions (nouvelle & voiture) fin de connaitre la direction
            var force = 1.10f + (float)Math.Sqrt(_latencyAverager.Average() / 2500) + (Speed / 250); // Calcul pour connaitre la force à appliquer à partir du ping & de la vitesse
            var forceVelo = 0.97f + (float)Math.Sqrt(_latencyAverager.Average() / 5000) + (Speed / 750); // calcul de la force à appliquer au vecteur

            if (MainVehicle.Velocity.Length() > VehicleVelocity.Length()) //
            {
                MainVehicle.Velocity = VehicleVelocity * forceVelo + (vecDif * 3f); // Calcul
            }
            else
            {
                MainVehicle.Velocity = VehicleVelocity * (forceVelo - 0.20f) + (vecDif * force); // Calcul
            }

            //StuckVehicleCheck(Position);

            if (_lastVehicleRotation != null && (_lastVehicleRotation.Value - _vehicleRotation).LengthSquared() > 1f)
            {
                MainVehicle.Quaternion = GTA.Math.Quaternion.Slerp(_lastVehicleRotation.Value.ToQuaternion(), _vehicleRotation.ToQuaternion(), Math.Min(1.5f, TicksSinceLastUpdate / (float)AverageLatency));
            }
            else
            {
                MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
            }
        }

        private void StuckVehicleCheck(Vector3 newPos)
        {
            #if !DISABLE_UNDER_FLOOR_FIX

            const int VEHICLE_INTERPOLATION_WARP_THRESHOLD = 15;
            const int VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED = 10;

            var fThreshold = (VEHICLE_INTERPOLATION_WARP_THRESHOLD + VEHICLE_INTERPOLATION_WARP_THRESHOLD_FOR_SPEED * Speed);

            if (MainVehicle.Position.DistanceToSquared(currentInterop.vecTarget) > fThreshold * fThreshold)
            {
                // Abort all interpolation
                currentInterop.FinishTime = 0;
                MainVehicle.PositionNoOffset = currentInterop.vecTarget;
            }
            
            // Check if we're under floor
            var bForceLocalZ = false;
            if (!MainVehicle.Model.IsHelicopter && !MainVehicle.Model.IsPlane)
            {
                // If remote z higher by too much and remote not doing any z movement, warp local z coord
                var fDeltaZ = newPos.Z - MainVehicle.Position.Z;

                if (fDeltaZ > 0.4f && fDeltaZ < 10.0f)
                {
                    if (Math.Abs(VehicleVelocity.Z) < 0.01f)
                    {
                        bForceLocalZ = true;
                    }
                }
            }

            // Only force z coord if needed for at least two consecutive calls
            if (!bForceLocalZ)
            {
                _mUiForceLocalZCounter = 0;
            }
            else if (_mUiForceLocalZCounter++ > 1)
            {
                var t = new Vector3(MainVehicle.Position.X, MainVehicle.Position.Y, newPos.Z);
                MainVehicle.PositionNoOffset = t;
                currentInterop.FinishTime = 0;
            }
#endif
        }

        private int _mUiForceLocalZCounter;
        private void DisplayVehiclePosition()
        {
            var spazzout = (_spazzout_prevention != null && DateTime.Now.Subtract(_spazzout_prevention.Value).TotalMilliseconds > 200);

            if ((Speed > 0.2f || IsInBurnout) && currentInterop.FinishTime > 0 && _lastPosition != null && spazzout)
            {
                VMultiVehiclePos();
                _stopTime = DateTime.Now;
                _carPosOnUpdate = MainVehicle.Position;
            }
            else if (DateTime.Now.Subtract(_stopTime).TotalMilliseconds <= 1000 && _lastPosition != null && spazzout && currentInterop.FinishTime > 0)
            {
                var dir = Position - _lastPosition.Value;
                var posTarget = Util.Util.LinearVectorLerp(_carPosOnUpdate, Position + dir, (int)DateTime.Now.Subtract(_stopTime).TotalMilliseconds, 1000);
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, MainVehicle, posTarget.X, posTarget.Y, posTarget.Z, 0, 0, 0, 0);
            }
            else
            {
                MainVehicle.PositionNoOffset = Position;
            }

#if !DISABLE_SLERP

            if (_lastVehicleRotation != null && (_lastVehicleRotation.Value - _vehicleRotation).LengthSquared() > 1f && spazzout)
            {
                MainVehicle.Quaternion = GTA.Math.Quaternion.Slerp(_lastVehicleRotation.Value.ToQuaternion(), _vehicleRotation.ToQuaternion(), Math.Min(1.5f, TicksSinceLastUpdate / (float)AverageLatency));
            }
            else
            {
                MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
            }
#else
            MainVehicle.Quaternion = _vehicleRotation.ToQuaternion();
#endif

        }

        private bool DisplayVehicleDriveBy()
        {
            if (!IsShooting || CurrentWeapon == 0 || VehicleSeat != -1 || !WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash) VehicleHash)) return false;

            var isRocket = WeaponDataProvider.IsVehicleWeaponRocket(CurrentWeapon);
            if (isRocket)
            {
                if (DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds < 1500) return true;
                _lastRocketshot = DateTime.Now;
            }

            var isParallel = WeaponDataProvider.DoesVehicleHaveParallelWeapon(unchecked((VehicleHash)VehicleHash), isRocket);

            var muzzle = WeaponDataProvider.GetVehicleWeaponMuzzle(unchecked((VehicleHash)VehicleHash), isRocket);

            if (isParallel && _leftSide)
            {
                muzzle = new Vector3(muzzle.X * -1f, muzzle.Y, muzzle.Z);
            }
            _leftSide = !_leftSide;

            var start = MainVehicle.GetOffsetInWorldCoords(muzzle);
            var end = start + Main.RotationToDirection(VehicleRotation) * 100f;
            var hash = CurrentWeapon;
            var speed = 0xbf800000;

            if (isRocket)
            {
                speed = 0xbf800000;
            }
            else if ((VehicleHash) VehicleHash == GTA.VehicleHash.Savage || (VehicleHash) VehicleHash == GTA.VehicleHash.Hydra || (VehicleHash) VehicleHash == GTA.VehicleHash.Lazer)
            {
                hash = unchecked((int) WeaponHash.Railgun);
            }
            else
            {
                hash = unchecked((int)WeaponHash.CombatPDW);
            }


            var damage = IsFriend() ? 0 : 75;

            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X, end.Y, end.Z, damage, true, hash, Character, true, false, speed);

            return false;
        }

        void UpdateVehicleMountedWeapon()
        {
            if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)VehicleHash, VehicleSeat))
            {
                var delay = 30;
                //if ((VehicleHash) VehicleHash == GTA.Native.VehicleHash.Rhino) delay = 300;

                if (Game.GameTime - _lastVehicleAimUpdate > delay)
                {
                    Function.Call(Hash.TASK_VEHICLE_AIM_AT_COORD, Character, AimCoords.X, AimCoords.Y,
                        AimCoords.Z);
                    _lastVehicleAimUpdate = Game.GameTime;
                }

                if (IsShooting)
                {
                    if (((VehicleHash)VehicleHash == GTA.VehicleHash.Rhino &&
                         DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds > 1000) ||
                        ((VehicleHash)VehicleHash != GTA.VehicleHash.Rhino))
                    {
                        _lastRocketshot = DateTime.Now;

                        var baseTurretPos =
                            MainVehicle.GetOffsetInWorldCoords(
                                WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash)VehicleHash, false));
                        var doesBaseTurretDiffer =
                            WeaponDataProvider.DoesVehiclesMuzzleDifferFromVehicleGunPos(
                                (VehicleHash)VehicleHash);
                        var barrellLength = WeaponDataProvider.GetVehicleTurretLength((VehicleHash)VehicleHash);

                        var speed = 0xbf800000;
                        var hash = WeaponHash.CombatPDW;
                        if ((VehicleHash)VehicleHash == GTA.VehicleHash.Rhino)
                        {
                            hash = (WeaponHash)1945616459;
                        }

                        Vector3 tPos = baseTurretPos;
                        if (
                            WeaponDataProvider.DoesVehicleHaveParallelWeapon((VehicleHash)VehicleHash, false) &&
                            VehicleSeat == 1)
                        {
                            var muzzle = WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash)VehicleHash,
                                false);
                            tPos =
                                MainVehicle.GetOffsetInWorldCoords(new Vector3(muzzle.X * -1f, muzzle.Y, muzzle.Z));
                        }

                        if (doesBaseTurretDiffer)
                        {
                            var kekDir = (AimCoords - tPos);
                            kekDir.Normalize();
                            var rot = Main.DirectionToRotation(kekDir);
                            var newDir = Main.RotationToDirection(new Vector3(0, 0, rot.Z));
                            newDir.Normalize();
                            tPos = tPos +
                                   newDir *
                                   WeaponDataProvider.GetVehicleWeaponMuzzle((VehicleHash)VehicleHash, true)
                                       .Length();
                        }


                        var turretDir = (AimCoords - tPos);
                        turretDir.Normalize();
                        var start = tPos + turretDir * barrellLength;
                        var end = start + turretDir * 100f;

                        _lastStart = start;
                        _lastEnd = end;

                        var damage = WeaponDataProvider.GetWeaponDamage(WeaponHash.Minigun);
                        if ((VehicleHash)VehicleHash == GTA.VehicleHash.Rhino)
                            damage = 210;

                        if (IsFriend())
                            damage = 0;

                        Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z, end.X,
                            end.Y, end.Z, damage, true, (int)hash, Character, true, false, speed);
                    }
                }
            }
            else if (!WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)VehicleHash) || VehicleSeat != -1)
            {
                if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon)
                {
                    //Function.Call(Hash.GIVE_WEAPON_TO_PED, Character, CurrentWeapon, 999, true, true);
                    //Function.Call(Hash.SET_CURRENT_PED_WEAPON, Character, CurrentWeapon, true);
                    //Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
                    //Character.Weapons.Select((WeaponHash) CurrentWeapon);
                    Character.Weapons.RemoveAll();
                    Character.Weapons.Give((WeaponHash)CurrentWeapon, -1, true, true);
                }

                if (IsShooting || IsAiming)
                {
                    if (!_lastDrivebyShooting)
                    {
                        Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, Character, false, false, false, false);

                        Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z,
                            0, 0, 0, unchecked((int)FiringPattern.SingleShot));
                    }
                    else
                    {
                        Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, Character, true, false, false, false);

                        Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z);
                    }

                    var rightSide = (VehicleSeat + 2) % 2 == 0;

                    if (WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
                    {
                        const string rightDict = "veh@driveby@first_person@passenger_right_handed@throw";
                        const string leftDict = "veh@driveby@first_person@driver@throw";

                        string drivebyDict = rightSide ? rightDict : leftDict;

                        Function.Call(Hash.TASK_PLAY_ANIM_ADVANCED, Character, Util.Util.LoadDict(drivebyDict),
                            "sweep_low", Character.Position.X, Character.Position.Y, Character.Position.Z, Character.Rotation.X,
                            Character.Rotation.Y, Character.Rotation.Z, -8f, -8f, -1, 0, rightSide ? 0.6f : 0.3f, 0, 0);
                    }

                    if (IsShooting)
                    {
                        Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, Character, true);
                        Function.Call(Hash.SET_PED_AMMO, Character, CurrentWeapon, 10);


                        if (AimPlayer != null && AimPlayer.Position != null)
                        {
                            AimCoords = AimPlayer.Position;
                            AimPlayer = null;
                        }

                        if (!WeaponDataProvider.NeedsFakeBullets(CurrentWeapon))
                        {
                            Function.Call(Hash.SET_PED_SHOOTS_AT_COORD, Character, AimCoords.X, AimCoords.Y, AimCoords.Z,
                                true);
                        }
                        else if (DateTime.Now.Subtract(_lastRocketshot).TotalMilliseconds > 500)
                        {
                            _lastRocketshot = DateTime.Now;

                            var damage = WeaponDataProvider.GetWeaponDamage((WeaponHash)CurrentWeapon);
                            var speed = 0xbf800000;
                            var weaponH = (WeaponHash)CurrentWeapon;

                            if (IsFriend())
                                damage = 0;

                            var start = Character.GetBoneCoord(rightSide ? Bone.SKEL_R_Hand : Bone.SKEL_L_Hand);
                            var end = AimCoords;

                            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z,
                                end.X,
                                end.Y, end.Z, damage, true, (int)weaponH, Character, false, true, speed);
                        }
                    }

                    _lastVehicleAimUpdate = Game.GameTime;
                    _lastDrivebyShooting = IsShooting || IsAiming;
                    Ped PlayerChar = Game.Player.Character;
                    if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, PlayerChar, Character, true))
                    {

                        int boneHit = -1;
                        var boneHitArg = new OutputArgument();

                        if (Function.Call<bool>(Hash.GET_PED_LAST_DAMAGE_BONE, PlayerChar, boneHitArg))
                        {
                            boneHit = boneHitArg.GetResult<int>();
                        }

                        LocalHandle them = new LocalHandle(Character.Handle, HandleType.GameHandle);
                        JavascriptHook.InvokeCustomEvent(api =>
                            api.invokeonLocalPlayerDamaged(them, CurrentWeapon, boneHit/*, playerHealth, playerArmor*/));
                    }

                    Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, Character);
                    Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, PlayerChar);
                }

                if (!IsShooting && !IsAiming && _lastDrivebyShooting && Game.GameTime - _lastVehicleAimUpdate > 200)
                {
                    Character.Task.ClearAll();
                    Character.Task.ClearSecondary();
                    Function.Call(Hash.CLEAR_DRIVEBY_TASK_UNDERNEATH_DRIVING_TASK, Character);
                    //Function.Call(Hash.TASK_DRIVE_BY, Character, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    //Function.Call(Hash.SET_DRIVEBY_TASK_TARGET, Character, 0, 0, 0, 0, 0);
                    Character.Task.ClearLookAt();
                    //GTA.UI.Screen.ShowNotification("Done shooting");
                    //GTA.UI.Screen.ShowSubtitle("Done Shooting1", 300);
                    _lastDrivebyShooting = false;
                }
            }
        }
    }
}