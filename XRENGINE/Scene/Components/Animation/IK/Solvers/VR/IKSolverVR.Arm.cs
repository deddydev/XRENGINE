﻿using Extensions;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using XREngine.Data;
using XREngine.Data.Colors;
using XREngine.Data.Core;
using XREngine.Scene.Transforms;
using static XREngine.Engine.Rendering.Debug;

namespace XREngine.Components.Animation
{
    public partial class IKSolverVR
    {
        /// <summary>
        /// 4-segmented analytic arm chain.
        /// </summary>
        [Serializable]
        public class ArmSolver(bool isLeft) : BodyPart
        {
            #region Parameters

            [Serializable]
            public enum EShoulderRotationMode
            {
                YawPitch,
                FromTo
            }

            private Transform? _target = null;
			/// <summary>
			/// The hand target.
            /// This should not be the hand controller itself, but a child GameObject parented to it so you could adjust its position/rotation to match the orientation of the hand bone.
            /// The best practice for setup would be to move the hand controller to the avatar's hand as it it was held by the avatar, duplicate the avatar's hand bone and parent it to the hand controller.
            /// Then assign the duplicate to this slot.
			/// </summary>
			public Transform? Target
            {
                get => _target;
                set => SetField(ref _target, value);
			}

			private float _positionWeight = 1.0f;
			/// <summary>
			/// Positional weight of the hand target.
            /// Note that if you have nulled the target, the hand will still be pulled to the last position of the target until you set this value to 0.
			/// </summary>
			[Range(0.0f, 1.0f)]
			public float PositionWeight
            {
                get => _positionWeight;
                set => SetField(ref _positionWeight, value);
            }

			private float _rotationWeight = 1.0f;
			/// <summary>
			/// Rotational weight of the hand target.
            /// Note that if you have nulled the target, the hand will still be rotated to the last rotation of the target until you set this value to 0.
			/// </summary>
			[Range(0.0f, 1.0f)]
			public float RotationWeight
            {
                get => _rotationWeight;
                set => SetField(ref _rotationWeight, value);
			}

			private float _shoulderRotationWeight = 1.0f;
			/// <summary>
			/// The weight of shoulder rotation.
			/// </summary>
			[Range(0.0f, 1.0f)]
			public float ShoulderRotationWeight
            {
                get => _shoulderRotationWeight;
                set => SetField(ref _shoulderRotationWeight, value);
			}

			private EShoulderRotationMode _shoulderRotationMode = EShoulderRotationMode.YawPitch;
			/// <summary>
			/// Different techniques for shoulder bone rotation.
			/// </summary>
			public EShoulderRotationMode ShoulderRotationMode
            {
                get => _shoulderRotationMode;
                set => SetField(ref _shoulderRotationMode, value);
            }

			private float _shoulderTwistWeight = 1.0f;
			/// <summary>
			/// The weight of twisting the shoulders backwards when arms are lifted up.
			/// </summary>
			public float ShoulderTwistWeight
            {
                get => _shoulderTwistWeight;
                set => SetField(ref _shoulderTwistWeight, value);
			}

			private float _shoulderYawOffset = 45.0f;
			/// <summary>
			/// Tweak this value to adjust shoulder rotation around the yaw (up) axis.
			/// </summary>
			public float ShoulderYawOffset
            {
                get => _shoulderYawOffset;
                set => SetField(ref _shoulderYawOffset, value);
            }

			private float _shoulderPitchOffset = -30.0f;
			/// <summary>
			/// Tweak this value to adjust shoulder rotation around the pitch (forward) axis.
			/// </summary>
			public float ShoulderPitchOffset
            {
                get => _shoulderPitchOffset;
                set => SetField(ref _shoulderPitchOffset, value);
			}

			private Transform? _bendGoal;
			/// <summary>
			/// The elbow will be bent towards this Transform if 'Bend Goal Weight' > 0.
			/// </summary>
			public Transform? BendGoal
            {
                get => _bendGoal;
                set => SetField(ref _bendGoal, value);
			}

			private float _bendGoalWeight;
			/// <summary>
			/// If greater than 0, will bend the elbow towards the 'Bend Goal' Transform.
			/// </summary>
			[Range(0.0f, 1.0f)]
			public float BendGoalWeight
            {
                get => _bendGoalWeight;
                set => SetField(ref _bendGoalWeight, value);
			}

			private float _swivelOffset;
			/// <summary>
			/// Angular offset of the elbow bending direction.
			/// </summary>
			[Range(-180.0f, 180.0f)]
			public float SwivelOffset
            {
                get => _swivelOffset;
                set => SetField(ref _swivelOffset, value);
			}

			private Vector3 _wristToPalmAxis = Vector3.Zero;
			/// <summary>
			/// Local axis of the hand bone that points from the wrist towards the palm.
			/// Used for defining hand bone orientation.
			/// Use <see cref="GuessHandOrientations(HumanoidComponent?, bool)"/> to guess the correct orientation of this axis.
			/// </summary>
			public Vector3 WristToPalmAxis
            {
                get => _wristToPalmAxis;
                set => SetField(ref _wristToPalmAxis, value);
            }

			private Vector3 _palmToThumbAxis = Vector3.Zero;
			/// <summary>
			/// Local axis of the hand bone that points from the palm towards the thumb.
			/// Used for defining hand bone orientation.
			/// Use <see cref="GuessHandOrientations(HumanoidComponent?, bool)"/> to guess the correct orientation of this axis.
			/// </summary>
			public Vector3 PalmToThumbAxis
            {
                get => _palmToThumbAxis;
                set => SetField(ref _palmToThumbAxis, value);
			}

			private float _armLengthScale = 1.0f;
			/// <summary>
			/// Use this to make the arm shorter/longer.
            /// Works by displacement of hand and forearm localPosition.
			/// </summary>
			[Range(0.01f, 2f)]
			public float ArmLengthScale
            {
                get => _armLengthScale;
                set => SetField(ref _armLengthScale, value);
			}

			/// <summary>
			/// 'Time' represents (target distance / arm length) and 'value' represents the amount of stretching.
            /// So value at time 1 represents stretching amount at the point where distance to the target is equal to arm length.
            /// Value at time 2 represents stretching amount at the point where distance to the target is double the arm length.
            /// Linear stretching would be achieved with a linear curve going up by 45 degrees.
            /// Increase the range of stretching by moving the last key up and right by the same amount.
            /// Smoothing in the curve can help reduce elbow snapping (start stretching the arm slightly before target distance reaches arm length).
			/// </summary>
			private AnimationCurve _stretchCurve = new();
            public AnimationCurve StretchCurve
            {
                get => _stretchCurve;
                set => SetField(ref _stretchCurve, value);
			}

			[NonSerialized]
			private Vector3 _ikPosition = Vector3.Zero;
			/// <summary>
			/// Target position of the hand.
			/// Will be overwritten if target is assigned.
			/// </summary>
			[HideInInspector]
			public Vector3 IKPosition
            {
                get => _ikPosition;
                set => SetField(ref _ikPosition, value);
			}

			[NonSerialized]
			private Quaternion _ikRotation = Quaternion.Identity;
			/// <summary>
			/// Target rotation of the hand.
			/// Will be overwritten if target is assigned.
			/// </summary>
			[HideInInspector]
			public Quaternion IKRotation
            {
                get => _ikRotation;
                set => SetField(ref _ikRotation, value);
            }

			[NonSerialized]
			private Vector3 _bendDirection = Globals.Backward;
			/// <summary>
			/// The bending direction of the limb.
			/// Will be used if bendGoalWeight is greater than 0.
			/// Will be overwritten if bendGoal is assigned.
			/// </summary>
			[HideInInspector]
			public Vector3 BendDirection
            {
                get => _bendDirection;
                set => SetField(ref _bendDirection, value);
			}

			[NonSerialized]
			private Vector3 _handPositionOffset;
			/// <summary>
			/// Position offset of the hand.
            /// Will be applied on top of hand target position and reset to Vector3.zero after each update.
			/// </summary>
			[HideInInspector]
			public Vector3 HandPositionOffset
            {
                get => _handPositionOffset;
                set => SetField(ref _handPositionOffset, value);
            }

			public Vector3 TargetPosition { get; private set; }
            public Quaternion TargetRotation { get; private set; }

            private bool _hasShoulder;

            private VirtualBone Shoulder => _bones[0];
            private VirtualBone UpperArm => _bones[_hasShoulder ? 1 : 0];
            private VirtualBone Forearm => _bones[_hasShoulder ? 2 : 1];
            private VirtualBone Hand => _bones[_hasShoulder ? 3 : 2];

            public override void Visualize(ColorF4 color)
            {
                base.Visualize(color);

                string side = isLeft ? "Left" : "Right";

                if (Target is not null)
                {
                    //RenderCoordinateSystem(Target.WorldMatrix);
                    RenderPoint(Target.RenderTranslation, ColorF4.Black);
                    RenderText(Target.RenderTranslation, $"{side} Hand Target", ColorF4.Black);
                }

                if (BendGoal != null)
                {
                    //RenderCoordinateSystem(BendGoal.WorldMatrix);
                    RenderPoint(BendGoal.RenderTranslation, ColorF4.Black);
                    RenderText(BendGoal.RenderTranslation, $"{side} Elbow Target", ColorF4.Black);
                }

                if (_hasShoulder)
                {
                    //RenderCoordinateSystem(Shoulder.SolverPosition, Shoulder.SolverRotation);
                    RenderText(Shoulder.SolverPosition, $"{side} Shoulder", ColorF4.Black);
                }

                //RenderCoordinateSystem(UpperArm.SolverPosition, UpperArm.SolverRotation);
                RenderText(UpperArm.SolverPosition, $"{side} Upper Arm", ColorF4.Black);

                //RenderCoordinateSystem(Forearm.SolverPosition, Forearm.SolverRotation);
                RenderText(Forearm.SolverPosition, $"{side} Forearm", ColorF4.Black);

                //RenderCoordinateSystem(Hand.SolverPosition, Hand.SolverRotation);
                RenderText(Hand.SolverPosition, $"{side} Hand", ColorF4.Black);
            }

            private Vector3 _chestForwardAxis;
            private Vector3 _chestUpAxis;
            private Quaternion _chestRotation = Quaternion.Identity;
            private Vector3 _chestForward;
            private Vector3 _chestUp;
            private Quaternion _forearmRelToUpperArm = Quaternion.Identity;
            private Vector3 _upperArmBendAxis;

            #endregion

            protected override void OnRead(SolverTransforms transforms)
            {
                if (_initialized)
                    return;

                var rootRotation = transforms.Root.InputWorld.Rotation;
                var side = isLeft ? transforms.Left : transforms.Right;
                var upperArmPosition = side.Arm.Arm.InputWorld.Translation;
                var upperArmRotation = side.Arm.Arm.InputWorld.Rotation;
                var forearmPosition = side.Arm.Elbow.InputWorld.Translation;
                var handPosition = side.Arm.Wrist.InputWorld.Translation;
                var handRotation = side.Arm.Wrist.InputWorld.Rotation;

                IKPosition = handPosition;
                IKRotation = handRotation;
                TargetRotation = IKRotation;

                if (_hasShoulder = transforms.HasShoulders)
                {
                    _bones =
                    [
                        new(side.Arm.Shoulder),
                        new(side.Arm.Arm),
                        new(side.Arm.Elbow),
                        new(side.Arm.Wrist),
                    ];
                }
                else
                {
                    _bones =
                    [
                        new(side.Arm.Arm),
                        new(side.Arm.Elbow),
                        new(side.Arm.Wrist),
                    ];
                }

                Vector3 rootFwd = rootRotation.Rotate(Globals.Forward);
                Vector3 rootUp = rootRotation.Rotate(Globals.Up);

                _chestForwardAxis = Quaternion.Inverse(_rootRotation).Rotate(rootFwd);
                _chestUpAxis = Quaternion.Inverse(_rootRotation).Rotate(rootUp);
                _upperArmBendAxis = GetUpperArmBendAxis(upperArmPosition, upperArmRotation, forearmPosition, rootFwd);

                if (_upperArmBendAxis == Vector3.Zero)
                    Debug.LogWarning(
                        "Cannot calculate which way to bend the arms because the arms are perfectly straight. " +
                        "Rotate the elbow bones slightly in their natural bending direction.");
            }

            private static Vector3 GetUpperArmBendAxis(Vector3 upperArmPosition, Quaternion upperArmRotation, Vector3 forearmPosition, Vector3 rootFwd)
            {
                // Get the local axis of the upper arm pointing towards the bend normal
                Vector3 upperArmForwardAxis = XRMath.GetAxisVectorToDirection(upperArmRotation, rootFwd);
                if (Vector3.Dot(upperArmRotation.Rotate(upperArmForwardAxis), rootFwd) < 0.0f)
                    upperArmForwardAxis = -upperArmForwardAxis;

                Vector3 upperArmToForearm = forearmPosition - upperArmPosition;
                return Vector3.Cross(upperArmForwardAxis, Quaternion.Inverse(upperArmRotation).Rotate(upperArmToForearm));
            }

            public override void PreSolve(float scale)
            {
                if (_target != null)
                {
                    _target.RecalculateMatrices(true);
                    _ikPosition = _target.WorldTranslation;
                    _ikRotation = _target.WorldRotation;
                }
                
                TargetPosition = Vector3.Lerp(Hand.SolverPosition, _ikPosition, _positionWeight);
                TargetRotation = Quaternion.Lerp(Hand.SolverRotation, _ikRotation, _rotationWeight);

                Shoulder.Axis = Shoulder.Axis.Normalized();
                _forearmRelToUpperArm = Quaternion.Inverse(UpperArm.SolverRotation) * Forearm.SolverRotation;
            }

            public override void ApplyOffsets(float scale)
                => TargetPosition += _handPositionOffset;

            private void StretchArm()
            {
                // Adjusting arm length
                float armLength = UpperArm.Length + Forearm.Length;
                Vector3 elbowAdd;
                Vector3 handAdd;

                if (_armLengthScale < 1.0f)
                {
                    armLength *= _armLengthScale;
                    elbowAdd = (Forearm.SolverPosition - UpperArm.SolverPosition) * (_armLengthScale - 1.0f);
                    handAdd = (Hand.SolverPosition - Forearm.SolverPosition) * (_armLengthScale - 1.0f);
                    Forearm.SolverPosition += elbowAdd;
                    Hand.SolverPosition += elbowAdd + handAdd;
                }

                // Stretching
                float distanceToTarget = Vector3.Distance(UpperArm.SolverPosition, TargetPosition);
                float stretchF = distanceToTarget / armLength;

                float m = _stretchCurve.Evaluate(stretchF);
                m *= _positionWeight;

                elbowAdd = (Forearm.SolverPosition - UpperArm.SolverPosition) * m;
                handAdd = (Hand.SolverPosition - Forearm.SolverPosition) * m;

                Forearm.SolverPosition += elbowAdd;
                Hand.SolverPosition += elbowAdd + handAdd;
            }

            public void Solve()
            {
                _chestRotation = XRMath.LookRotation(_rootRotation.Rotate(_chestForwardAxis), _rootRotation.Rotate(_chestUpAxis));
                _chestForward = _chestRotation.Rotate(Globals.Forward);
                _chestUp = _chestRotation.Rotate(Globals.Up);

                //RenderLine(Shoulder.SolverPosition, Shoulder.SolverPosition + _chestForward, ColorF4.Blue);
                //RenderLine(Shoulder.SolverPosition, Shoulder.SolverPosition + _chestUp, ColorF4.Green);

                Vector3 bendNormal = SolveTrigonometric();
                SetUpperArmRotation(bendNormal);
                SetHandRotation();
            }

            private void SetHandRotation()
            {
                if (_rotationWeight >= 1.0f)
                    Hand.SolverRotation = TargetRotation;
                else if (_rotationWeight > 0.0f)
                    Hand.SolverRotation = Quaternion.Lerp(Hand.SolverRotation, TargetRotation, _rotationWeight);
            }

            private void SetUpperArmRotation(Vector3 bendNormal)
            {
                if (Quality >= EQuality.Semi || _positionWeight <= 0.0f)
                    return;
                
                // Fix upperarm twist relative to bend normal
                Vector3 forward = UpperArm.SolverRotation.Rotate(_upperArmBendAxis);
                Vector3 up = Forearm.SolverPosition - UpperArm.SolverPosition;
                Quaternion space = XRMath.LookRotation(forward, up);

                Vector3 upperArmTwist = Quaternion.Inverse(space).Rotate(bendNormal);
                float angleDeg = float.RadiansToDegrees(MathF.Atan2(upperArmTwist.X, upperArmTwist.Z));
                Vector3 upperArmToForearm = Forearm.SolverPosition - UpperArm.SolverPosition;
                UpperArm.SolverRotation = Quaternion.CreateFromAxisAngle(upperArmToForearm, float.DegreesToRadians(angleDeg * _positionWeight)) * UpperArm.SolverRotation;

                // Fix forearm twist relative to upper arm
                Quaternion forearmFixed = UpperArm.SolverRotation * _forearmRelToUpperArm;
                Vector3 from = forearmFixed.Rotate(Forearm.Axis);
                Vector3 to = Hand.SolverPosition - Forearm.SolverPosition;
                Quaternion fromTo = XRMath.RotationBetweenVectors(from, to);
                RotateTo(Forearm, fromTo * forearmFixed, _positionWeight);
            }

            private Vector3 SolveTrigonometric()
            {
                Vector3 bendNormal;
                if (_hasShoulder && _shoulderRotationWeight > 0.0f && Quality < EQuality.Semi)
                    FullShoulderSolve(out bendNormal);
                else
                    NoShoulderSolve(out bendNormal);
                return bendNormal;
            }

            private void NoShoulderSolve(out Vector3 bendNormal)
            {
                if (Quality < EQuality.Semi)
                    StretchArm();

                bendNormal = GetBendNormal();
                int firstBoneIndex = _hasShoulder ? 1 : 0;
                VirtualBone.SolveTrigonometric(
                    _bones,
                    firstBoneIndex,
                    firstBoneIndex + 1,
                    firstBoneIndex + 2,
                    TargetPosition,
                    bendNormal,
                    _positionWeight);
            }

            private void FullShoulderSolve(out Vector3 bendNormal)
            {
                switch (_shoulderRotationMode)
                {
                    default:
                    case EShoulderRotationMode.YawPitch:
                        {
                            bendNormal = ShoulderYawPitch();
                            break;
                        }
                    case EShoulderRotationMode.FromTo:
                        {
                            bendNormal = ShoulderFromTo();
                            break;
                        }
                }
            }

            private Vector3 ShoulderFromTo()
            {
                Vector3 bendNormal;
                Quaternion shoulderRotation = Shoulder.SolverRotation;
                Vector3 shoulderToUpperArm = UpperArm.SolverPosition - Shoulder.SolverPosition;
                Vector3 shoulderToTarget = TargetPosition - Shoulder.SolverPosition;

                Quaternion r = XRMath.RotationBetweenVectors(
                    shoulderToUpperArm.Normalized() + _chestForward,
                    shoulderToTarget);

                float weight = 0.5f * _shoulderRotationWeight * _positionWeight;
                r = Quaternion.Slerp(Quaternion.Identity, r, weight);

                VirtualBone.RotateBy(_bones, r);

                StretchArm();

                Vector3 shoulderToForearm = Forearm.SolverPosition - Shoulder.SolverPosition;
                Vector3 shoulderToHand = Hand.SolverPosition - Shoulder.SolverPosition;
                bendNormal = Vector3.Cross(shoulderToForearm, shoulderToHand);

                //Solve shoulder-elbow-hand to target
                VirtualBone.SolveTrigonometric(_bones, 0, 2, 3, TargetPosition, bendNormal, weight);

                bendNormal = GetBendNormal();

                //Solve arm-elbow-hand to target
                VirtualBone.SolveTrigonometric(_bones, 1, 2, 3, TargetPosition, bendNormal, _positionWeight);

                // Twist shoulder and upper arm bones when holding hands up
                Quaternion q = Quaternion.Inverse(XRMath.LookRotation(_chestUp, _chestForward));
                Vector3 vBefore = q.Rotate(shoulderRotation.Rotate(Shoulder.Axis));
                Vector3 vAfter = q.Rotate(Shoulder.SolverRotation.Rotate(Shoulder.Axis));

                float angleBefore = float.RadiansToDegrees(MathF.Atan2(vBefore.X, vBefore.Z));
                float angleAfter = float.RadiansToDegrees(MathF.Atan2(vAfter.X, vAfter.Z));
                float pitchAngle = XRMath.DeltaAngle(angleBefore, angleAfter);

                if (isLeft)
                    pitchAngle = -pitchAngle;

                pitchAngle = (pitchAngle * _shoulderRotationWeight * _shoulderTwistWeight * 2.0f * _positionWeight).Clamp(0.0f, 180.0f);

                Vector3 shoulderAxis = Shoulder.SolverRotation.Rotate(isLeft ? Shoulder.Axis : -Shoulder.Axis);
                Vector3 upperArmAxis = UpperArm.SolverRotation.Rotate(isLeft ? UpperArm.Axis : -UpperArm.Axis);

                Shoulder.SolverRotation = Quaternion.CreateFromAxisAngle(shoulderAxis, pitchAngle) * Shoulder.SolverRotation;
                UpperArm.SolverRotation = Quaternion.CreateFromAxisAngle(upperArmAxis, pitchAngle) * UpperArm.SolverRotation;

                return bendNormal;
            }

            private Vector3 ShoulderYawPitch()
            {
                CalcYaw(out float yawDeg, out Quaternion yawRotation);
                CalcPitch(out float pitchDeg, out Quaternion pitchRotation);

                // Rotate bones
                Quaternion shoulderRotation = pitchRotation * yawRotation;
                if (_shoulderRotationWeight * _positionWeight < 1.0f)
                    shoulderRotation = Quaternion.Lerp(Quaternion.Identity, shoulderRotation, _shoulderRotationWeight * _positionWeight);
                VirtualBone.RotateBy(_bones, shoulderRotation);

                StretchArm();

                Vector3 bendNormal = GetBendNormal();
                VirtualBone.SolveTrigonometric(_bones, 1, 2, 3, TargetPosition, bendNormal, _positionWeight);

                float pitchRad = float.DegreesToRadians((pitchDeg * _positionWeight * _shoulderRotationWeight * _shoulderTwistWeight * 2.0f).Clamp(0.0f, 180.0f));
                Shoulder.SolverRotation = Quaternion.CreateFromAxisAngle(Shoulder.SolverRotation.Rotate(isLeft ? Shoulder.Axis : -Shoulder.Axis), pitchRad) * Shoulder.SolverRotation;
                UpperArm.SolverRotation = Quaternion.CreateFromAxisAngle(UpperArm.SolverRotation.Rotate(isLeft ? UpperArm.Axis : -UpperArm.Axis), pitchRad) * UpperArm.SolverRotation;

                // Additional pass to reach with the shoulders
                //VirtualBone.SolveTrigonometric(_bones, 0, 1, 3, TargetPosition, Vector3.Cross(UpperArm.SolverPosition - Shoulder.SolverPosition, Hand.SolverPosition - Shoulder.SolverPosition), _positionWeight * 0.5f);
                return bendNormal;
            }

            private void CalcPitch(out float pitchDeg, out Quaternion pitchRotation)
            {
                Quaternion pitchOffset = Quaternion.CreateFromAxisAngle(
                    _chestUp,
                    float.DegreesToRadians(isLeft ? 90.0f : -90.0f));

                Quaternion workingSpace = pitchOffset * _chestRotation;

                workingSpace = Quaternion.CreateFromAxisAngle(
                    _chestForward,
                    isLeft ? -_shoulderPitchOffset : _shoulderPitchOffset) * workingSpace;

                Vector3 chestDir = _chestRotation.Rotate(isLeft ? Globals.Right : Globals.Left) * Length;
                Vector3 shoulderPos = Shoulder.SolverPosition + chestDir;
                Vector3 shoulderToTarget = TargetPosition - shoulderPos;
                Vector3 shoulderToTargetWorkingSpace = Quaternion.Inverse(workingSpace).Rotate(shoulderToTarget);

                pitchDeg = float.RadiansToDegrees(MathF.Atan2(shoulderToTargetWorkingSpace.Y, shoulderToTargetWorkingSpace.Z));
                pitchDeg -= _shoulderPitchOffset;
                //pitchDeg = DamperValue(pitchDeg, -45f - _shoulderPitchOffset, 45f - _shoulderPitchOffset);

                pitchRotation = Quaternion.CreateFromAxisAngle(workingSpace.Rotate(Globals.Right), float.DegreesToRadians(-pitchDeg));
            }

            private void CalcYaw(out float yawDeg, out Quaternion yawRotation)
            {
                float yawOffsetDeg = isLeft ? -_shoulderYawOffset : _shoulderYawOffset;
                float yawOffsetRad = float.DegreesToRadians((isLeft ? 90.0f : -90.0f) + yawOffsetDeg);
                Quaternion yawOffset = Quaternion.CreateFromAxisAngle(_chestUp, yawOffsetRad);
                Quaternion workingSpace = yawOffset * _chestRotation;

                Vector3 shoulderToTarget = (TargetPosition - Shoulder.SolverPosition).Normalized();
                Vector3 shoulderToTargetWorkingSpace = Quaternion.Inverse(workingSpace).Rotate(shoulderToTarget);

                yawDeg = float.RadiansToDegrees(MathF.Atan2(shoulderToTargetWorkingSpace.X, shoulderToTargetWorkingSpace.Z));

                float dotY = Vector3.Dot(shoulderToTargetWorkingSpace, Globals.Up);
                dotY = 1.0f - MathF.Abs(dotY);
                yawDeg *= dotY;

                yawDeg -= yawOffsetDeg;
                //float yawLimitMin = isLeft ? -20.0f : -50.0f;
                //float yawLimitMax = isLeft ? 50.0f : 20.0f;
                //yawDeg = DamperValue(yawDeg, yawLimitMin - yawOffsetDeg, yawLimitMax - yawOffsetDeg, 0.7f); // back, forward

                Quaternion yawQuat = Quaternion.CreateFromAxisAngle(Globals.Up, float.DegreesToRadians(yawDeg));

                Vector3 yawFromDir = Shoulder.SolverRotation.Rotate(Shoulder.Axis);
                Vector3 yawToDir = workingSpace.Rotate(yawQuat.Rotate(Globals.Forward));
                yawRotation = XRMath.RotationBetweenVectors(yawFromDir, yawToDir);
            }

            public override void ResetOffsets()
            {
                _handPositionOffset = Vector3.Zero;
            }

            private static float DamperValue(float value, float min, float max, float weight = 1.0f)
            {
                float range = max - min;

                if (weight < 1.0f)
                {
                    float mid = max - range * 0.5f;
                    float v = value - mid;
                    v *= 0.5f;
                    value = mid + v;
                }

                value -= min;

                float t = (value / range).Clamp(0.0f, 1.0f);
                float tEased = Interp.Float(t, EFloatInterpolationMode.InOutQuintic);
                return Interp.Lerp(min, max, tEased);
            }

            private Vector3 GetBendNormal()
            {
                Vector3 upperArmToTarget = TargetPosition - UpperArm.SolverPosition;

                if (_bendGoal != null)
                    _bendDirection = _bendGoal.WorldTranslation - _bones[1].SolverPosition;

                Vector3 armDir = _bones[0].SolverRotation.Rotate(_bones[0].Axis);

                Vector3 f = Globals.Down;
                Vector3 t = Quaternion.Inverse(_chestRotation).Rotate(upperArmToTarget.Normalized()) + Globals.Forward;
                Quaternion q = XRMath.RotationBetweenVectors(f, t);

                Vector3 b = q.Rotate(Globals.Backward);

                f = Quaternion.Inverse(_chestRotation).Rotate(armDir);
                t = Quaternion.Inverse(_chestRotation).Rotate(upperArmToTarget);
                q = XRMath.RotationBetweenVectors(f, t);
                b = q.Rotate(b);

                b = _chestRotation.Rotate(b);

                b += armDir;
                b -= TargetRotation.Rotate(_wristToPalmAxis);
                b -= TargetRotation.Rotate(_palmToThumbAxis) * 0.5f;

                if (_bendGoalWeight > 0.0f)
                    b = XRMath.Slerp(b, _bendDirection, _bendGoalWeight);

                if (_swivelOffset != 0.0f)
                    b = Quaternion.CreateFromAxisAngle(-upperArmToTarget, float.DegreesToRadians(_swivelOffset)).Rotate(b);

                return Vector3.Cross(upperArmToTarget, b);
            }

            private static void Visualize(VirtualBone bone1, VirtualBone bone2, VirtualBone bone3, ColorF4 color)
            {
                RenderLine(bone1.SolverPosition, bone2.SolverPosition, color);
                RenderLine(bone2.SolverPosition, bone3.SolverPosition, color);
            }
        }
    }
}
