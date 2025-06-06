using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Autohand {
    public enum HeldAnimationDriver {
        squeeze,
        grip,
        custom
    }

    [DefaultExecutionOrder(100000)]
    public class GrabbablePoseAnimaion : MonoBehaviour {
        public AnimationCurve animationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Determines the default hand value to activate this pose while it's being held")]
        public HeldAnimationDriver animationDriver = HeldAnimationDriver.squeeze;

        [NaughtyAttributes.ShowIf("animationDriver", HeldAnimationDriver.custom)]
        public float customValue;
        [Space]
        [Tooltip("The pose the hand will have by default")]
        public GrabbablePose fromPose;
        [Tooltip("The pose the hand will move to match given the animation driver value")]
        public GrabbablePose toPose;
        [Tooltip("Additional animations to run alongside the given driver value (good for things like a gun trigger that is separate from the hand but still needs to move with the hand during the animation)")]
        public AutoAnimation[] additionalAnimations;
        [Space]
        [Tooltip("The weight of the index finger in the animation - 0 means the finger will not animate, 1 means it will animate fully")]
        public float indexWeight = 1;
        [Tooltip("The weight of the middle finger in the animation - 0 means the finger will not animate, 1 means it will animate fully")]
        public float middleWeight = 1;
        [Tooltip("The weight of the ring finger in the animation - 0 means the finger will not animate, 1 means it will animate fully")]
        public float ringWeight = 1;
        [Tooltip("The weight of the pinky finger in the animation - 0 means the finger will not animate, 1 means it will animate fully")]
        public float pinkyWeight = 1;
        [Tooltip("The weight of the thumb finger in the animation - 0 means the finger will not animate, 1 means it will animate fully")]
        public float thumbWeight = 1;
        [Space]
        [Tooltip("The strength of the hand position lerping between the two poses")]
        public float handPositionWeight = 0;
        [Tooltip("The strength of the hand rotation lerping between the two poses")]
        public float handRotationWeight = 0;

        [Space]

        HandPoseData fromPoseData, toPoseData;
        HandPoseData _currentAnimationPose;

        float[] fingerWeights;

        public ref HandPoseData currentAnimationPose { get { return ref _currentAnimationPose; } }
        int lastPosingHandsCount;

        Dictionary<Hand, bool> trackWasIKEnabled = new Dictionary<Hand, bool>();

        private void OnEnable() {
            fingerWeights = new float[5];
            fingerWeights[(int)FingerEnum.index] = indexWeight;
            fingerWeights[(int)FingerEnum.middle] = middleWeight;
            fingerWeights[(int)FingerEnum.ring] = ringWeight;
            fingerWeights[(int)FingerEnum.pinky] = pinkyWeight;
            fingerWeights[(int)FingerEnum.thumb] = thumbWeight;

            if(fromPose.rightPoseSet)
                currentAnimationPose = new HandPoseData(ref fromPose.rightPose);
            else if(fromPose.leftPoseSet)
                currentAnimationPose = new HandPoseData(ref fromPose.leftPose);

            trackWasIKEnabled = new Dictionary<Hand, bool>();

        }



        public void LateUpdate() {
            var posingHandCount = fromPose.posingHands.Count + toPose.posingHands.Count;
            if(posingHandCount == 0)
                return;

            foreach(var hand in fromPose.posingHands) {
                if(hand.IsGrabbing())
                    continue;
                Animate(hand);
            }
            foreach(var hand in toPose.posingHands) {
                if(hand.IsGrabbing())
                    continue;
                Animate(hand);
            }

            if(lastPosingHandsCount != 0 && posingHandCount == 0)
                foreach(var autoAnim in additionalAnimations)
                    autoAnim.SetAnimation(0);

            lastPosingHandsCount = posingHandCount;

        }   

        public void Animate(Hand hand) {
            fromPoseData = fromPose.GetHandPoseData(hand);
            toPoseData = toPose.GetHandPoseData(hand);
            var animationValue = GetAnimationValue(hand);

            foreach(var finger in hand.fingers) {
                var fingerIndex = (int)finger.fingerType;
                if(fingerWeights[fingerIndex] == 0)
                    continue;

                currentAnimationPose.fingerPoses[fingerIndex].LerpData(ref fromPoseData.fingerPoses[fingerIndex], ref toPoseData.fingerPoses[fingerIndex], fingerWeights[fingerIndex] * animationValue);
                currentAnimationPose.fingerPoses[fingerIndex].SetFingerPose(finger);
            }

            if(handPositionWeight != 0 || handRotationWeight != 0) {
                currentAnimationPose.handOffset = Vector3.Lerp(fromPoseData.handOffset, toPoseData.handOffset, animationValue * handPositionWeight);
                currentAnimationPose.localQuaternionOffset = Quaternion.Lerp(fromPoseData.localQuaternionOffset, toPoseData.localQuaternionOffset, animationValue * handRotationWeight);
                hand.handGrabPoint.localRotation = currentAnimationPose.localQuaternionOffset;
                hand.handGrabPoint.localPosition = currentAnimationPose.handOffset;
            }

            foreach(var autoAnim in additionalAnimations)
                autoAnim.SetAnimation(animationCurve.Evaluate(animationValue));

            float GetAnimationValue(Hand hand1) {
                if(animationDriver == HeldAnimationDriver.squeeze)
                    return hand1.GetSqueezeAxis();
                else if(animationDriver == HeldAnimationDriver.grip)
                    return hand1.GetGripAxis();
                else if(animationDriver == HeldAnimationDriver.custom)
                    return customValue;

                return 0;
            }

        }



        public void Animate(Hand hand, float value) {
            HandPoseData.LerpPose(ref currentAnimationPose, ref fromPose.GetHandPoseData(hand), ref toPose.GetHandPoseData(hand), value);
            currentAnimationPose.handOffset = Vector3.Lerp(fromPoseData.handOffset, toPoseData.handOffset, value);
            currentAnimationPose.localQuaternionOffset = Quaternion.Lerp(fromPoseData.localQuaternionOffset, toPoseData.localQuaternionOffset, value);
            currentAnimationPose.SetPose(hand);
        }
    }

}