﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Modules
{
    /// <summary>
    /// The detectable types of spatial input that a node can perform
    /// </summary>
    [Flags]
    public enum SpatialInputType
    {
        None = 0, // 0
        X = 1 << 0, // 1 / Allow for flag-based polling of which axis' are involved in either a drag or rotation.
        Y = 1 << 1, // 2 / Euler's used in order to allow for polling either for translation or rotation
        Z = 1 << 2, // 4
        DragTranslation = 1 << 3, // 8
        SingleAxisRotation = (X ^ Y ^ Z), // Validate that only one axis is being rotated
        FreeRotation = (X & Y) | (Z & Y) | (Z & X) + 1 << 4, // Can be either 0/1. Detect at least two axis' crossing their local rotation threshold, triggers ray-based selection
        YawLeft = 1 << 5,
        YawRight = 1 << 6,
        PitchForward = 1 << 7,
        PitchBackward = 1 << 8,
        RollLeft = 1 << 9,
        RollRight = 1 << 10
    }

    public sealed class SpatialInputDetectionModule : MonoBehaviour
    {
        public class SpatialInputData : INodeToRay
        {
            public SpatialInputData(Node node, IDetectSpatialInputType caller)
            {
                rayOrigin = this.RequestRayOriginFromNode(node);
                m_Callers.Add(caller);

                initialPosition = rayOrigin.position;
                initialLocalRotation = rayOrigin.localRotation;
                spatialInputType = SpatialInputType.None;
            }

            private SpatialInputType m_SpatialInputType;

            // Collection housing caller objects requesting that this node be evaluated
            readonly List<IDetectSpatialInputType> m_Callers = new List<IDetectSpatialInputType>();

            /// <summary>
            /// The ray origin on which this spatial scroll is being processed
            /// </summary>
            public Transform rayOrigin { get; set; }

            /// <summary>
            /// Signed starting-position-dependent direction
            /// Each time an input type change occurs, this value is then recalculated from the new local input direction type
            /// </summary>
            public float signedDeltaMagnitude { get; set; }

            /// <summary>
            /// The origin/starting input position
            /// </summary>
            public Vector3 initialPosition { get; set; }

            /// <summary>
            /// The current input position
            /// </summary>
            public Vector3 currentPosition { get { return rayOrigin.position; } }

            /// <summary>
            /// The origin/starting input rotation
            /// </summary>
            public Quaternion initialLocalRotation { get; set; }

            /// <summary>
            /// The current input rotation
            /// </summary>
            public Quaternion currentLocalRotation { get { return rayOrigin.localRotation; } }

            public float CurrenLocalXRotation { get { return rayOrigin.localRotation.eulerAngles.x; } }
            public float CurrenLocalYRotation { get { return rayOrigin.localRotation.eulerAngles.y; } }
            public float CurrenLocalZRotation { get { return rayOrigin.localRotation.eulerAngles.z; } }

            /// <summary>
            /// Value representing how much of the pre-scroll drag amount has occurred
            /// </summary>
            public float dragDistance { get; set; }

            /// <summary>
            /// Bool denoting that the scroll trigger magnitude has been exceeded
            /// </summary>
            public bool inputTypeChanged { get; set; }

            public void AddCaller(IDetectSpatialInputType caller)
            {
                m_Callers.Add(caller);
            }

            public bool RemoveCaller(IDetectSpatialInputType caller)
            {
                var noCallersRemain = false;
                foreach (var existingCaller in m_Callers)
                {
                    if (existingCaller == caller)
                    {
                        m_Callers.Remove(caller);
                        break;
                    }
                }

                return m_Callers.Count > 0;
            }

            public bool beingPolled
            {
                get
                {
                    var beingPolledByCaller = false;
                    foreach (var caller in m_Callers)
                    {
                        if (caller.pollingSpatialInputType)
                        {
                            beingPolledByCaller = true;
                            break;
                        }
                    }

                    return beingPolledByCaller;
                }
            }

            public bool stateChangedThisFrame { get; set; }

            /// <summary>
            /// Current evaluated spatial input type being performed by this node
            /// </summary>
            public SpatialInputType spatialInputType
            {
                get { return m_SpatialInputType; }

                set
                {
                    if (m_SpatialInputType == value)
                    {
                        stateChangedThisFrame = false;
                        return;
                    }

                    stateChangedThisFrame = true;
                    m_SpatialInputType = value;
                    if (m_SpatialInputType == SpatialInputType.None)
                        return;

                    // A new ACTIVE state has been set
                    // Cache new relevant transform values for further comparision/validation
                    initialPosition = rayOrigin.position;
                    initialLocalRotation = rayOrigin.localRotation;
                }
            }
        }

        // Serialized Field region
        [SerializeField]
        HapticPulse m_TranslationPulse; // The pulse performed on a node performing a spatial scroll while only in translation mode
        
        [SerializeField]
        HapticPulse m_FreeRotationPulse; // The pulse performed on a node performing a spatial scroll while only in free-rotation mode
        
        [SerializeField]
        HapticPulse m_SingleAxistRotationPulse; // The pulse performed on a node performing a spatial scroll while only in single-axis rotation mode

        // Collection housing objects whose spatial input is being processed
        readonly Dictionary<Node, SpatialInputData> m_SpatialNodeData = new Dictionary<Node, SpatialInputData>();

        // Perform a constant haptic for translation/dragging
        // Perform a sharply pulsing haptic for rotation
        // Perform a gradual pulsing for free rotation
        // Monitor and perform the relevant pulses for all registered nodes, when spatial scrolling is being performed by that node

        /// <summary>
        /// Initiate spatial input processing for a given node & caller
        /// </summary>
        /// <param name="caller">Object requesting that a given node be tracked</param>
        /// <param name="node">Node whose input will be processed.  A caller may track multiple nodes.</param>
        public void BeginSpatialInputDetection(IDetectSpatialInputType caller, Node node)
        {
            SpatialInputData existingData = null;
            foreach (var nodeData in m_SpatialNodeData)
            {
                if (nodeData.Key == node)
                {
                    existingData = nodeData.Value;
                    break;
                }
            }

            if (existingData != null)
            {
                existingData.AddCaller(caller);
            }
            else
            {
                // Create a new KVP for a node not currently being processed
                // Additional callers can be added to a node's correspondng SpatialInputData
                var newTrackedObjectData = new SpatialInputData(node, caller);
                m_SpatialNodeData.Add(node, newTrackedObjectData);
            }
        }

        public void EndSpatialInputDetection(IDetectSpatialInputType caller, Node node)
        {
            // remove caller from any spatialInputData objects referencing this caller
            // If no callers remain for a node, remove the corresponding entry from the SpatialNodeData collection
            foreach (var nodeData in m_SpatialNodeData)
            {
                if (nodeData.Key == node)
                {
                    var spatialInputData = nodeData.Value;
                    var noCallersRemaining = spatialInputData.RemoveCaller(caller);
                    if (noCallersRemaining)
                        m_SpatialNodeData.Remove(node);

                    break;
                }
            }
        }

        void Update()
        {
            // Iterate over all ACTIVE(performing spatial input) nodes perform a spatial scroll
            // Update the SpatialInputType for each ACTIVE node
            // Set SpatialInputType for nodes not performing any spatial input to NONE
            // Otherwise, set relevant SpatialInputType value

            foreach (var nodeToSpatialData in m_SpatialNodeData)
            {
                var spatialInputData = nodeToSpatialData.Value;
                if (!spatialInputData.beingPolled)
                {
                    // Spatial input is NOT being performed on this node
                    spatialInputData.spatialInputType = SpatialInputType.None;
                }
                else
                {
                    // New initial position & rotation was just set
                    // Skip further evaluation for this data this frame for efficiency; evaluate next frame
                    if (spatialInputData.stateChangedThisFrame)
                        return;

                    // Order tests based on the active spatial input type of the node
                    switch (spatialInputData.spatialInputType)
                    {
                        case SpatialInputType.None:
                        case SpatialInputType.DragTranslation:
                            if (isNodeRotatingSingleAxis(spatialInputData))
                                continue;

                            if (isNodeRotatingFreely(spatialInputData))
                                continue;

                            break;
                        case SpatialInputType.SingleAxisRotation:
                            if (isNodeTranslating(spatialInputData))
                                continue;

                            if (isNodeRotatingFreely(spatialInputData))
                                continue;

                            break;
                        case SpatialInputType.FreeRotation:
                            if (isNodeTranslating(spatialInputData))
                                continue;

                            if (isNodeRotatingSingleAxis(spatialInputData))
                                continue;

                            break;
                    }
                }
            }
        }

        bool isNodeTranslating(SpatialInputData nodeData)
        {
            // set spatialInputType state
            return false;
        }

        bool isNodeRotatingSingleAxis(SpatialInputData nodeData)
        {
            // Test each individual axis delta for residing below the given threshold
            // If more than 1 tests beyond the threshold, set isTorationFreely to true, and isTranslating to false, then return false here

            // Ordered by usage priority Z(roll), X(pitch), then Y(yaw)
            // test z
            // test X
            // test Y

            var simultaneousAxisRotationCount = 0;
            simultaneousAxisRotationCount += PerformSingleAxisRotationTest(nodeData.initialLocalRotation.x, nodeData.CurrenLocalXRotation) ? 1 : 0;
            simultaneousAxisRotationCount += PerformSingleAxisRotationTest(nodeData.initialLocalRotation.y, nodeData.CurrenLocalYRotation) ? 1 : 0;

            // don't perform if this is going to be evaluated as a free rotation, due to multi-axis threshold crossing
            if (simultaneousAxisRotationCount < 2)
                simultaneousAxisRotationCount += PerformSingleAxisRotationTest(nodeData.initialLocalRotation.z, nodeData.CurrenLocalZRotation) ? 1 : 0;

            switch (simultaneousAxisRotationCount)
            {
                    case 1:
                        nodeData.spatialInputType = SpatialInputType.SingleAxisRotation;
                        return true;
                        break;
                    case 2:
                    case 3:
                        nodeData.spatialInputType = SpatialInputType.FreeRotation;
                        break;
            }

            return false;
        }

        bool isNodeRotatingFreely(SpatialInputData nodeData)
        {
            // Test each individual axis delta for residing below the given threshold
            // If more than 1 tests beyond the threshold, set isTorationFreely to true, and isTranslating to false, then return false here

            // Ordered by usage priority Z(roll), X(pitch), then Y(yaw)
            // test z
            // test X
            // test Y

            // set spatialInputType state
            return false;
        }

        bool PerformSingleAxisRotationTest(float initialSingleAxisRotationValue, float currentSingleAxisRotationValue)
        {
            const float kRotationThreshold = 0.3f; // Estimated wrist rotation threshold
            var deltaAngle = Mathf.Abs(Mathf.DeltaAngle(initialSingleAxisRotationValue, currentSingleAxisRotationValue));
            var aboveThreshold = deltaAngle > kRotationThreshold;
            return aboveThreshold;
        }

        public SpatialInputData GetSpatialInputTypeForNode(IDetectSpatialInputType obj, Node node)
        {
            // Iterate on the node to active state collection
            // Return none for those not performing a spatial input action
            // Return the relevant SpatialInputType for a given node otherwise

            SpatialInputData spatialInputType = null;
            foreach (var nodeToInputType in m_SpatialNodeData)
            {
                if (nodeToInputType.Key == node)
                {
                    spatialInputType = nodeToInputType.Value;
                    break;
                }
            }
            
            return spatialInputType;
        }
    }
}
#endif
