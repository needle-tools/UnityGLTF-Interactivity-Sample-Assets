using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animation))]
public class FillAnimationClip : MonoBehaviour
{
    public string Data = "";

    public float timePerCharacter = 0.1f;

    private void CreateClip()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "TestClip";
        clip.hideFlags = HideFlags.DontSave;
        clip.legacy = true;
        var curve = new AnimationCurve();
        float time = 0f;
        
        for (int i = 0; i < Data.Length; i++)
        {
            var character = Data[i];
            curve.AddKey(new Keyframe(i, (int)character));
            Debug.Log(">" + (int)character + "<");
            time += timePerCharacter;
        }
        
        clip.SetCurve("", typeof(Transform), "localPosition.y", curve);
        var ani = GetComponent<Animation>();
        ani.clip = clip;
        ani.RemoveClip("TestClip");
        ani.AddClip(clip, "TestClip");
    }

    private void OnValidate()
    {
        if (enabled)
            CreateClip();
    }
}
