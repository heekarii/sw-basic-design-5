using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Repair : MonoBehaviour
{
    private TransitionManager _transitionManager;
    [SerializeField] private bool _isEntered = false;

    private void Start()
    {
        #if UNITY_2023_2_OR_NEWER
        _transitionManager = UnityEngine.Object.FindFirstObjectByType<TransitionManager>();
        #else
        _transitionManager = FindObjectOfType<TransitionManager>();
        #endif
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision Detected");
        if (collision.gameObject.CompareTag("Player") && !_isEntered)
        {
            _transitionManager.EnterRepairStation(this);
        }
    }
    
    public void SetEnter(bool state)
    {
        _isEntered = state;
    }
}