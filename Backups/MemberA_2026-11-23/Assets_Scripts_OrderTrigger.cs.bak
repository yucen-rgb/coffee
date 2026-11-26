using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrderTrigger : MonoBehaviour
{
     public GameObject interactButton;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            interactButton.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            interactButton.SetActive(false);
        }
    }

}
