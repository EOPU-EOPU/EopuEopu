using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that sends user-defined information set in the inspector to the Fish shader for fish animation.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Renderer))]
    public class FishAnimation : MonoBehaviour
    {
        [SerializeField] private bool useBoneMouth;
        [SerializeField] private string lowerJawName;
        [SerializeField] private string upperJawName;
        [Tooltip("Body bone that separate jaw branches follow while opening and closing.")]
        [SerializeField] private string mouthFollowBoneName;
        private Transform mouthFollowBone;
        private Vector3 lowerJawRestPosition, upperJawRestPosition;
        private Vector3 lowerJawHeadPosition, upperJawHeadPosition;
        private Quaternion lowerJawHeadRotation, upperJawHeadRotation;
        [SerializeField, Range(0f, 25f)] private float mouthOpenAngle = 8f;
        [SerializeField, Range(0f, 0.8f)] private float closedHold = 0.25f;
        private Transform lowerJaw, upperJaw;
        private Quaternion lowerJawRest, upperJawRest;
        private Vector3 lowerJawAxis, upperJawAxis;
        private int mouthClosed = -1;

        #region General Settings

        public enum Axes
        {
            None, X, Y, Z
        }

        [Tooltip("Tint color for the material.")]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Identifier for main distortion axis.")]
        [SerializeField] private Axes mainDistortAxis = Axes.X;
        [Tooltip("Identifier for secondary distortion axis.")]
        [SerializeField] private Axes secondaryDistortAxis = Axes.Y;

        [Tooltip("Toggle to visualize main distortion wave gradient.")]
        [SerializeField] private bool visualizeMainWaveGradient = false;
        [Tooltip("Toggle to visualize secondary distortion wave gradient.")]
        [SerializeField] private bool visualizeSeconaryWaveGradient = false;

        #endregion

        #region Wave 1 Settings

        [Range(0.1f, 50), Tooltip("Length of main distortion wave.")]
        [SerializeField] private float waveLength1 = 6.5f;
        [Range(0, 3), Tooltip("Speed of main distortion wave.")]
        [SerializeField] private float waveSpeed1 = 2;
        [Range(0, 3), Tooltip("Amplitude of main distortion wave.")]
        [SerializeField] private float waveHeight1 = .2f;
        [Range(0, 1), Tooltip("The blending/feathering between where the main distortion wave starts and stops.")]
        [SerializeField] private float gradientBlending1 = 1;
        [Tooltip("Offset/displacement value of the main distortion gradient.")]
        [SerializeField] private float gradientOffset1 = 0;

        #endregion

        #region Wave 2 Settings

        [Range(0.1f, 50), Tooltip("Length of secondary distortion wave.")]
        [SerializeField] private float waveLength2 = 6.5f;
        [Range(0, 3), Tooltip("Speed of secondary distortion wave.")]
        [SerializeField] private float waveSpeed2 = 2;
        [Range(0, 3), Tooltip("Amplitude of secondary distortion wave.")]
        [SerializeField] private float waveHeight2 = .2f;
        [Range(0, 1), Tooltip("The blending/feathering between where the secondary distortion wave starts and stops.")]
        [SerializeField] private float gradientBlending2 = 1;
        [Tooltip("Offset/displacement value of the secondary distortion gradient.")]
        [SerializeField] private float gradientOffset2 = 0;

        #endregion

        #region Misc. Settings

        [Tooltip("Minimum and maximum clamp values for main and secondary distortion gradients.")]
        [SerializeField] private Vector2 clampMinMax = new Vector2(-.1f, 3);
        [Range(0, 100), Tooltip("Chance for the animation's properties to have greater variation from the above values in-game. The higher the value the greater the chance of variation.")]
        [SerializeField] private float randomizationPercent = 50;

        [Header("Bone Body Wave")]
        [Tooltip("Use the skinned mesh's body bones to create one continuous S-shaped swimming motion.")]
        [SerializeField] private bool useBoneBodyWave = false;
        [Tooltip("Body bones ordered from head to tail. Fin bones should not be included; they inherit the body motion from their parents.")]
        [SerializeField] private string[] bodyBoneNames = { "bone_1", "bone_2", "bone_3", "bone_4", "bone_5", "bone_8", "bone_9" };
        [Range(0.1f, 5f), Tooltip("Number of swimming cycles per second.")]
        [SerializeField] private float bodyWaveSpeed = 1.15f;
        [Range(0f, 20f), Tooltip("Maximum local rotation applied near the tail. Keep this modest because rotations accumulate down the bone chain.")]
        [SerializeField] private float bodyBendAngle = 7f;
        [Range(0.25f, 2f), Tooltip("Number of S-wave cycles distributed from head to tail.")]
        [SerializeField] private float bodyWaveCycles = 0.85f;
        [Range(0f, 1f), Tooltip("Fraction of the tail bend applied at the head. Zero keeps the head stable.")]
        [SerializeField] private float headBendRatio = 0.08f;
        [Range(1f, 3f), Tooltip("Extra bend for the last two tail bones, including tail sections that are weighted only to the second-to-last bone.")]
        [SerializeField] private float tailBendMultiplier = 1.6f;
        [Tooltip("The second tail branch, ordered from its base to its tip.")]
        [SerializeField] private string[] lowerTailBoneNames = { "bone_6", "bone_7" };
        [Tooltip("Separate head branch that must follow the first body bone so the eyes and mouth move with the head.")]
        [SerializeField] private string headAttachmentBoneName = "bone_16";

        [Header("Mouth Animation")]
        [Tooltip("Animate a mouth blend shape independently from the body wave.")]
        [SerializeField] private bool useMouthAnimation = false;
        [Tooltip("Name of the blend shape used to open the mouth.")]
        [SerializeField] private string mouthBlendShapeName = "MouthOpen";
        [Range(0.1f, 5f), Tooltip("Number of mouth opening cycles per second.")]
        [SerializeField] private float mouthSpeed = 1.2f;
        [Range(0f, 100f), Tooltip("Maximum blend shape weight applied while the mouth is open.")]
        [SerializeField] private float mouthOpenAmount = 65f;

        #endregion

        #region Private Fields

        private MaterialPropertyBlock propBlock;
        private Renderer rend;

        // Adds randomness to select properties. Computed in Start
        private float random = 0;
        private Transform[] bodyBones;
        private Quaternion[] bodyBoneRestRotations;
        private Transform[] lowerTailBones;
        private Quaternion[] lowerTailRestRotations;
        private Transform headAttachmentBone;
        private Quaternion headAttachmentRestRotation;
        private SkinnedMeshRenderer skinnedRenderer;
        private int mouthBlendShapeIndex = -1;

        #endregion

        #region Unity Callbacks

        private void Awake() 
        {
            rend = GetComponent<Renderer>();
        }

        private void Start()
        {
            // Randomness constant
            random = Random.Range(-randomizationPercent / 100, randomizationPercent / 100);

            //Make sure material property block is updated on start
            UpdatePropBlock(random);
            CacheBodyBones();
            CacheMouthBlendShape();
        }

        private void OnEnable()
        {
            rend = GetComponent<Renderer>();
            skinnedRenderer = rend as SkinnedMeshRenderer;
            if (!skinnedRenderer || !skinnedRenderer.sharedMesh) return;
            var available = skinnedRenderer.bones;
            lowerJaw = System.Array.Find(available, b => b && b.name == lowerJawName);
            upperJaw = System.Array.Find(available, b => b && b.name == upperJawName);
            mouthFollowBone = System.Array.Find(available, b => b && b.name == mouthFollowBoneName);
            if (lowerJaw)
            {
                lowerJawRest = lowerJaw.localRotation;
                lowerJawRestPosition = lowerJaw.localPosition;
                if (mouthFollowBone)
                {
                    lowerJawHeadPosition = mouthFollowBone.InverseTransformPoint(lowerJaw.position);
                    lowerJawHeadRotation = Quaternion.Inverse(mouthFollowBone.rotation) * lowerJaw.rotation;
                }
                lowerJawAxis = lowerJaw.InverseTransformDirection(transform.TransformDirection(Vector3.right)).normalized;
            }
            if (upperJaw)
            {
                upperJawRest = upperJaw.localRotation;
                upperJawRestPosition = upperJaw.localPosition;
                if (mouthFollowBone)
                {
                    upperJawHeadPosition = mouthFollowBone.InverseTransformPoint(upperJaw.position);
                    upperJawHeadRotation = Quaternion.Inverse(mouthFollowBone.rotation) * upperJaw.rotation;
                }
                upperJawAxis = upperJaw.InverseTransformDirection(transform.TransformDirection(Vector3.right)).normalized;
            }
            mouthClosed = skinnedRenderer.sharedMesh.GetBlendShapeIndex("MouthClosed");
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (useBoneBodyWave)
            {
                if (bodyBones == null || bodyBones.Length == 0)
                    CacheBodyBones();

                if (bodyBones != null && bodyBones.Length > 0)
                {
                    float timePhase = Time.time * bodyWaveSpeed * Mathf.PI * 2f;
                    int lastIndex = Mathf.Max(1, bodyBones.Length - 1);

                    for (int i = 0; i < bodyBones.Length; i++)
                    {
                        if (!bodyBones[i])
                            continue;

                        float alongBody = i / (float)lastIndex;
                        float amplitude = bodyBendAngle * Mathf.Lerp(headBendRatio, 1f, alongBody * alongBody);
                        if (i >= bodyBones.Length - 2)
                            amplitude *= tailBendMultiplier;
                        float phase = timePhase - alongBody * bodyWaveCycles * Mathf.PI * 2f;
                        float angle = Mathf.Sin(phase) * amplitude;
                        bodyBones[i].localRotation = bodyBoneRestRotations[i] * Quaternion.AngleAxis(angle, Vector3.forward);
                    }

                    if (headAttachmentBone && bodyBones[0])
                    {
                        Quaternion headDelta = bodyBones[0].localRotation * Quaternion.Inverse(bodyBoneRestRotations[0]);
                        headAttachmentBone.localRotation = headDelta * headAttachmentRestRotation;
                    }

                    if (lowerTailBones != null)
                    {
                        for (int i = 0; i < lowerTailBones.Length; i++)
                        {
                            if (!lowerTailBones[i])
                                continue;

                            float alongBody = lowerTailBones.Length == 1 ? 1f : Mathf.Lerp(0.83f, 1f, i / (float)(lowerTailBones.Length - 1));
                            float phase = timePhase - alongBody * bodyWaveCycles * Mathf.PI * 2f;
                            float angle = Mathf.Sin(phase) * bodyBendAngle * tailBendMultiplier;
                            lowerTailBones[i].localRotation = Quaternion.AngleAxis(angle, Vector3.forward) * lowerTailRestRotations[i];
                        }
                    }
                }
            }

            UpdateMouthAnimation();
        }

        private void OnDisable()
        {
            RestoreBodyBones();
            ResetMouthBlendShape();
            if (useBoneMouth)
            {
                if (lowerJaw) lowerJaw.localRotation = lowerJawRest;
                if (upperJaw) upperJaw.localRotation = upperJawRest;
                if (lowerJaw) lowerJaw.localPosition = lowerJawRestPosition;
                if (upperJaw) upperJaw.localPosition = upperJawRestPosition;
                if (skinnedRenderer && mouthClosed >= 0) skinnedRenderer.SetBlendShapeWeight(mouthClosed, 0f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!rend)
                rend = GetComponent<Renderer>();

            // Update material property block to transfer data to shader whenever something is changed in the inspector
            UpdatePropBlock(random);
        }
#endif

        #endregion

        private void UpdatePropBlock(float rand)
        {
            // Initialize material property block
            propBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propBlock);

            propBlock.SetColor("_Tint", tint);

            // Gradient visualization
            propBlock.SetFloat("_VisualizeGradient1", visualizeMainWaveGradient ? 1 : 0);
            propBlock.SetFloat("_VisualizeGradient2", visualizeSeconaryWaveGradient ? 1 : 0);

            // Primary axis distortion properties
            propBlock.SetFloat("_WaveLength1", waveLength1 + (waveLength1 * rand));
            propBlock.SetFloat("_WaveSpeed1", waveSpeed1 + (waveSpeed1 * rand));
            propBlock.SetFloat("_WaveHeight1", useBoneBodyWave ? 0f : waveHeight1 + (waveHeight1 * rand));
            propBlock.SetFloat("_GradBlending1", gradientBlending1);
            propBlock.SetFloat("_GradOffset1", gradientOffset1);
            
            // Secondary axis distortion properties
            propBlock.SetFloat("_WaveLength2", waveLength2 + (waveLength2 * rand));
            propBlock.SetFloat("_WaveSpeed2", waveSpeed2 + (waveSpeed2 * rand));
            propBlock.SetFloat("_WaveHeight2", useBoneBodyWave ? 0f : waveHeight2 + (waveHeight2 * rand));
            propBlock.SetFloat("_GradBlending2", gradientBlending2);
            propBlock.SetFloat("_GradOffset2", gradientOffset2);
            
            // Distortion gradient clamping
            propBlock.SetFloat("_ClampMin", clampMinMax.x);
            propBlock.SetFloat("_ClampMax", clampMinMax.y);
            
            // Set main distortion axis
            switch (useBoneBodyWave ? Axes.None : mainDistortAxis)
            {
                case Axes.X:
                    propBlock.SetFloat("_AxisNum1", 1);
                    break;
                case Axes.Y:
                    propBlock.SetFloat("_AxisNum1", 2);
                    break;
                case Axes.Z:
                    propBlock.SetFloat("_AxisNum1", 3);
                    break;
                default:
                    propBlock.SetFloat("_AxisNum1", 0);
                    break;
            }

            // Set secondary distortion axis
            switch (useBoneBodyWave ? Axes.None : secondaryDistortAxis)
            {
                case Axes.X:
                    propBlock.SetFloat("_AxisNum2", 1);
                    break;
                case Axes.Y:
                    propBlock.SetFloat("_AxisNum2", 2);
                    break;
                case Axes.Z:
                    propBlock.SetFloat("_AxisNum2", 3);
                    break;
                default:
                    propBlock.SetFloat("_AxisNum2", 0);
                    break;
            }

            // Set material property block
            rend.SetPropertyBlock(propBlock);
        }

        private void CacheBodyBones()
        {
            RestoreBodyBones();

            skinnedRenderer = rend as SkinnedMeshRenderer;
            if (!skinnedRenderer || bodyBoneNames == null || bodyBoneNames.Length == 0)
            {
                bodyBones = null;
                bodyBoneRestRotations = null;
                return;
            }

            Transform[] availableBones = skinnedRenderer.bones;
            bodyBones = new Transform[bodyBoneNames.Length];
            bodyBoneRestRotations = new Quaternion[bodyBoneNames.Length];

            for (int i = 0; i < bodyBoneNames.Length; i++)
            {
                for (int j = 0; j < availableBones.Length; j++)
                {
                    if (availableBones[j] && availableBones[j].name == bodyBoneNames[i])
                    {
                        bodyBones[i] = availableBones[j];
                        bodyBoneRestRotations[i] = availableBones[j].localRotation;
                        break;
                    }
                }
            }

            int lowerTailCount = lowerTailBoneNames == null ? 0 : lowerTailBoneNames.Length;
            lowerTailBones = new Transform[lowerTailCount];
            lowerTailRestRotations = new Quaternion[lowerTailCount];
            for (int i = 0; i < lowerTailBoneNames.Length; i++)
            {
                for (int j = 0; j < availableBones.Length; j++)
                {
                    if (availableBones[j] && availableBones[j].name == lowerTailBoneNames[i])
                    {
                        lowerTailBones[i] = availableBones[j];
                        lowerTailRestRotations[i] = availableBones[j].localRotation;
                        break;
                    }
                }
            }

            for (int i = 0; i < availableBones.Length; i++)
            {
                if (availableBones[i] && availableBones[i].name == headAttachmentBoneName)
                {
                    headAttachmentBone = availableBones[i];
                    headAttachmentRestRotation = availableBones[i].localRotation;
                    break;
                }
            }
        }

        private void CacheMouthBlendShape()
        {
            skinnedRenderer = rend as SkinnedMeshRenderer;
            mouthBlendShapeIndex = -1;

            if (!skinnedRenderer || !skinnedRenderer.sharedMesh || string.IsNullOrEmpty(mouthBlendShapeName))
                return;

            mouthBlendShapeIndex = skinnedRenderer.sharedMesh.GetBlendShapeIndex(mouthBlendShapeName);
        }

        private void UpdateMouthAnimation()
        {
            if (!useMouthAnimation)
                return;

            if (useBoneMouth)
            {
                float cycle = Mathf.Repeat(Time.time * mouthSpeed, 1f);
                float opening = cycle <= closedHold ? 0f :
                    Mathf.Pow(Mathf.Sin(Mathf.PI * (cycle - closedHold) / (1f - closedHold)), 2f);
                if (lowerJaw) lowerJaw.localRotation = lowerJawRest * Quaternion.AngleAxis(opening * mouthOpenAngle, lowerJawAxis);
                if (upperJaw) upperJaw.localRotation = upperJawRest * Quaternion.AngleAxis(-opening * mouthOpenAngle * 0.3f, upperJawAxis);
                if (mouthFollowBone)
                {
                    if (lowerJaw) lowerJaw.SetPositionAndRotation(
                        mouthFollowBone.TransformPoint(lowerJawHeadPosition),
                        mouthFollowBone.rotation * lowerJawHeadRotation * Quaternion.AngleAxis(opening * mouthOpenAngle, lowerJawAxis));
                    if (upperJaw) upperJaw.SetPositionAndRotation(
                        mouthFollowBone.TransformPoint(upperJawHeadPosition),
                        mouthFollowBone.rotation * upperJawHeadRotation * Quaternion.AngleAxis(-opening * mouthOpenAngle * 0.3f, upperJawAxis));
                }
                if (skinnedRenderer && mouthClosed >= 0) skinnedRenderer.SetBlendShapeWeight(mouthClosed, (1f - opening) * 100f);
                return;
            }

            if (!skinnedRenderer || mouthBlendShapeIndex < 0)
                CacheMouthBlendShape();

            if (!skinnedRenderer || mouthBlendShapeIndex < 0)
                return;

            float openPhase = Mathf.Sin(Time.time * mouthSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            float openWeight = Mathf.SmoothStep(0f, 1f, openPhase) * mouthOpenAmount;
            skinnedRenderer.SetBlendShapeWeight(mouthBlendShapeIndex, openWeight);
        }

        private void ResetMouthBlendShape()
        {
            if (skinnedRenderer && mouthBlendShapeIndex >= 0)
                skinnedRenderer.SetBlendShapeWeight(mouthBlendShapeIndex, 0f);
        }

        private void RestoreBodyBones()
        {
            if (bodyBones == null || bodyBoneRestRotations == null)
                return;

            int count = Mathf.Min(bodyBones.Length, bodyBoneRestRotations.Length);
            for (int i = 0; i < count; i++)
            {
                if (bodyBones[i])
                    bodyBones[i].localRotation = bodyBoneRestRotations[i];
            }

            if (lowerTailBones == null || lowerTailRestRotations == null)
                return;

            count = Mathf.Min(lowerTailBones.Length, lowerTailRestRotations.Length);
            for (int i = 0; i < count; i++)
            {
                if (lowerTailBones[i])
                    lowerTailBones[i].localRotation = lowerTailRestRotations[i];
            }

            if (headAttachmentBone)
                headAttachmentBone.localRotation = headAttachmentRestRotation;
        }
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for FishAnimation to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(FishAnimation), true), CanEditMultipleObjects, System.Serializable]
    public class FishAnimation_Editor : Editor
    {
        SerializedProperty tint, mainDistortAxis, secondaryDistortAxis, visualizeMainWaveGradient, visualizeSeconaryWaveGradient, waveLength1, waveSpeed1, waveHeight1, gradientBlending1, gradientOffset1, waveLength2, waveSpeed2, waveHeight2, gradientBlending2, gradientOffset2,
            clampMinMax, randomizationPercent, useBoneBodyWave, bodyBoneNames, bodyWaveSpeed, bodyBendAngle, bodyWaveCycles, headBendRatio, tailBendMultiplier, lowerTailBoneNames, headAttachmentBoneName,
            useMouthAnimation, mouthBlendShapeName, mouthSpeed, mouthOpenAmount;

        private bool generalFoldout = true;
        private bool wave1Foldout = true;
        private bool wave2Foldout = true;
        private bool miscFoldout = true;
        private bool boneBodyWaveFoldout = true;
        private bool mouthAnimationFoldout = true;

        private void OnEnable()
        {
            #region Seriealized Property Initialization

            tint = serializedObject.FindProperty("tint");
            mainDistortAxis = serializedObject.FindProperty("mainDistortAxis");
            secondaryDistortAxis = serializedObject.FindProperty("secondaryDistortAxis");

            visualizeMainWaveGradient = serializedObject.FindProperty("visualizeMainWaveGradient");
            visualizeSeconaryWaveGradient = serializedObject.FindProperty("visualizeSeconaryWaveGradient");

            waveLength1 = serializedObject.FindProperty("waveLength1");
            waveSpeed1 = serializedObject.FindProperty("waveSpeed1");
            waveHeight1 = serializedObject.FindProperty("waveHeight1");
            gradientBlending1 = serializedObject.FindProperty("gradientBlending1");
            gradientOffset1 = serializedObject.FindProperty("gradientOffset1");

            waveLength2 = serializedObject.FindProperty("waveLength2");
            waveSpeed2 = serializedObject.FindProperty("waveSpeed2");
            waveHeight2 = serializedObject.FindProperty("waveHeight2");
            gradientBlending2 = serializedObject.FindProperty("gradientBlending2");
            gradientOffset2 = serializedObject.FindProperty("gradientOffset2");

            clampMinMax = serializedObject.FindProperty("clampMinMax");
            randomizationPercent = serializedObject.FindProperty("randomizationPercent");
            useBoneBodyWave = serializedObject.FindProperty("useBoneBodyWave");
            bodyBoneNames = serializedObject.FindProperty("bodyBoneNames");
            bodyWaveSpeed = serializedObject.FindProperty("bodyWaveSpeed");
            bodyBendAngle = serializedObject.FindProperty("bodyBendAngle");
            bodyWaveCycles = serializedObject.FindProperty("bodyWaveCycles");
            headBendRatio = serializedObject.FindProperty("headBendRatio");
            tailBendMultiplier = serializedObject.FindProperty("tailBendMultiplier");
            lowerTailBoneNames = serializedObject.FindProperty("lowerTailBoneNames");
            headAttachmentBoneName = serializedObject.FindProperty("headAttachmentBoneName");
            useMouthAnimation = serializedObject.FindProperty("useMouthAnimation");
            mouthBlendShapeName = serializedObject.FindProperty("mouthBlendShapeName");
            mouthSpeed = serializedObject.FindProperty("mouthSpeed");
            mouthOpenAmount = serializedObject.FindProperty("mouthOpenAmount");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((FishAnimation)target), typeof(FishAnimation), false);
            GUI.enabled = true;

            #region General Settings

            generalFoldout = EditorGUILayout.Foldout(generalFoldout, "General Settings");

            if (generalFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(tint);

                GUILayout.Space(10);

                EditorGUILayout.PropertyField(mainDistortAxis);
                EditorGUILayout.PropertyField(secondaryDistortAxis);

                GUILayout.Space(10);

                EditorGUILayout.PropertyField(visualizeMainWaveGradient);
                EditorGUILayout.PropertyField(visualizeSeconaryWaveGradient);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Wave 1 Settings

            wave1Foldout = EditorGUILayout.Foldout(wave1Foldout, "Wave 1 Settings");

            if (wave1Foldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(waveLength1);
                EditorGUILayout.PropertyField(waveSpeed1);
                EditorGUILayout.PropertyField(waveHeight1);
                EditorGUILayout.PropertyField(gradientBlending1);
                EditorGUILayout.PropertyField(gradientOffset1);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Wave 2 Settings

            wave2Foldout = EditorGUILayout.Foldout(wave2Foldout, "Wave 2 Settings");

            if (wave2Foldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(waveLength2);
                EditorGUILayout.PropertyField(waveSpeed2);
                EditorGUILayout.PropertyField(waveHeight2);
                EditorGUILayout.PropertyField(gradientBlending2);
                EditorGUILayout.PropertyField(gradientOffset2);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Misc. Settings

            miscFoldout = EditorGUILayout.Foldout(miscFoldout, "Misc. Settings");

            if (miscFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(clampMinMax);
                EditorGUILayout.PropertyField(randomizationPercent);

                EditorGUI.indentLevel--;
            }

            #endregion

            boneBodyWaveFoldout = EditorGUILayout.Foldout(boneBodyWaveFoldout, "Bone Body Wave");

            if (boneBodyWaveFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(useBoneBodyWave);
                EditorGUILayout.PropertyField(bodyBoneNames, true);
                EditorGUILayout.PropertyField(bodyWaveSpeed);
                EditorGUILayout.PropertyField(bodyBendAngle);
                EditorGUILayout.PropertyField(bodyWaveCycles);
                EditorGUILayout.PropertyField(headBendRatio);
                EditorGUILayout.PropertyField(tailBendMultiplier);
                EditorGUILayout.PropertyField(lowerTailBoneNames, true);
                EditorGUILayout.PropertyField(headAttachmentBoneName);
                EditorGUI.indentLevel--;
            }

            mouthAnimationFoldout = EditorGUILayout.Foldout(mouthAnimationFoldout, "Mouth Animation");

            if (mouthAnimationFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(useMouthAnimation);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useBoneMouth"));
                if (serializedObject.FindProperty("useBoneMouth").boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("lowerJawName"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("upperJawName"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("mouthFollowBoneName"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("mouthOpenAngle"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("closedHold"));
                }
                EditorGUILayout.PropertyField(mouthBlendShapeName);
                EditorGUILayout.PropertyField(mouthSpeed);
                EditorGUILayout.PropertyField(mouthOpenAmount);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            
            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }
    }
#endif
}
