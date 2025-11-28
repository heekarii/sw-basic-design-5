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
        _transitionManager = FindObjectOfType<TransitionManager>();
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision Detected");
        if (collision.gameObject.CompareTag("Player") && !_isEntered)
        {
            _transitionManager.EnterRepairStation();
            _isEntered = true;
        }
    }
}