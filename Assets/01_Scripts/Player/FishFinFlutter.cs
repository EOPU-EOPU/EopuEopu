using System;
using UnityEngine;

/// <summary>Species-independent fin bone motion and mesh membrane ripples.</summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(SkinnedMeshRenderer))]
public sealed class FishFinFlutter : MonoBehaviour
{
    [Serializable]
    public sealed class FinBone
    {
        public string boneName;
        [Tooltip("Rotation axes in the mesh object's local coordinates.")]
        public Vector3 flapAxis = Vector3.up;
        public Vector3 sweepAxis = Vector3.forward;
        [Tooltip("Signed angle in degrees. Opposite signs mirror left/right fins.")]
        public float flapAngle = 12f;
        public float sweepAngle = 3f;
        [Tooltip("Phase delay in radians; increase toward the fin tip.")]
        public float phaseLag;
    }

    [Header("Fin Bones")]
    [Tooltip("Optional fin bones. Empty uses membrane ripples only. Body and jaw bones belong in FishAnimation.")]
    [SerializeField] private FinBone[] finBones = Array.Empty<FinBone>();
    [SerializeField, Range(0.1f, 4f)] private float frequency = 1.35f;

    [Header("Fin Membranes")]
    [SerializeField] private string rippleSinName = "FinRippleSin";
    [SerializeField] private string rippleCosName = "FinRippleCos";
    [SerializeField, Range(0f, 2f)] private float membraneStrength = 1f;
    [SerializeField, Range(0.1f, 4f)] private float membraneFrequency = 1.7f;

    private struct FinState
    {
        public FinBone settings;
        public Transform bone;
        public Quaternion restRotation;
        public Vector3 flapAxis;
        public Vector3 sweepAxis;
    }

    private SkinnedMeshRenderer skin;
    private FinState[] states = Array.Empty<FinState>();
    private int rippleSin = -1;
    private int rippleCos = -1;

    private void OnEnable()
    {
        skin = GetComponent<SkinnedMeshRenderer>();
        rippleSin = skin.sharedMesh ? skin.sharedMesh.GetBlendShapeIndex(rippleSinName) : -1;
        rippleCos = skin.sharedMesh ? skin.sharedMesh.GetBlendShapeIndex(rippleCosName) : -1;
        var available = skin.bones;
        states = new FinState[finBones == null ? 0 : finBones.Length];
        for (int i = 0; i < states.Length; i++)
        {
            var settings = finBones[i];
            if (settings == null || string.IsNullOrEmpty(settings.boneName)) continue;
            var bone = Array.Find(available, b => b && b.name == settings.boneName);
            if (!bone) continue;
            bool duplicate = false;
            for (int j = 0; j < i; j++) duplicate |= states[j].bone == bone;
            if (duplicate) continue;
            states[i] = new FinState
            {
                settings = settings,
                bone = bone,
                restRotation = bone.localRotation,
                flapAxis = bone.InverseTransformDirection(transform.TransformDirection(settings.flapAxis)).normalized,
                sweepAxis = bone.InverseTransformDirection(transform.TransformDirection(settings.sweepAxis)).normalized
            };
        }
    }

    private void LateUpdate()
    {
        float ripplePhase = Time.time * membraneFrequency * Mathf.PI * 2f;
        if (rippleSin >= 0) skin.SetBlendShapeWeight(rippleSin, Mathf.Sin(ripplePhase) * 100f * membraneStrength);
        if (rippleCos >= 0) skin.SetBlendShapeWeight(rippleCos, Mathf.Cos(ripplePhase) * 100f * membraneStrength);
        float phase = Time.time * frequency * Mathf.PI * 2f;
        foreach (var state in states)
        {
            if (!state.bone) continue;
            var settings = state.settings;
            float p = phase - settings.phaseLag;
            float wave = Mathf.Sin(p) + 0.16f * Mathf.Sin(p * 2f + 0.7f);
            state.bone.localRotation = state.restRotation
                * Quaternion.AngleAxis(settings.flapAngle * wave, state.flapAxis)
                * Quaternion.AngleAxis(settings.sweepAngle * Mathf.Sin(p - 0.65f), state.sweepAxis);
        }
    }

    private void OnDisable()
    {
        if (skin && rippleSin >= 0) skin.SetBlendShapeWeight(rippleSin, 0f);
        if (skin && rippleCos >= 0) skin.SetBlendShapeWeight(rippleCos, 0f);
        foreach (var state in states)
            if (state.bone) state.bone.localRotation = state.restRotation;
    }
}
