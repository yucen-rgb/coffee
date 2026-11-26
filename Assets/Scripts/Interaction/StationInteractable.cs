
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace CoffeeVR.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class StationInteractable : MonoBehaviour
    {
        [Header("Station Settings")]
        [SerializeField] private string stationId;
        [SerializeField] private XRSimpleInteractable simpleInteractable;
        [SerializeField] private RefrigeratorDoorController refrigeratorDoorController;
        [SerializeField] private GameObject promptCanvas;

        [Header("Oven UI")]
        [SerializeField] private GameObject ovenProgressCanvas;
        [SerializeField] private Image ovenLoadingImage;
        [SerializeField] private GameObject ovenFinishCanvas;

        [Header("Coffee Machine UI")]
        [SerializeField] private GameObject coffeeMachineProgressCanvas;
        [SerializeField] private Image coffeeMachineLoadingImage;
        [SerializeField] private GameObject coffeeMachineFinishCanvas;

        [Header("Operation Sound Effects")]
        [SerializeField] private AudioSource operationAudioSource;

        [SerializeField] private AudioClip ovenOperationSound;
        [SerializeField] private AudioClip coffeeMachineOperationSound;
        [SerializeField] private AudioClip finishSound;

        [Header("Oven Animation Settings")]
        [SerializeField] private float rotationDuration = 2.5f;
        [SerializeField] private float finishDuration = 5f;

        private bool isOvenBusy;
        private Coroutine ovenCoroutine;
        private Coroutine coffeeMachineCoroutine;

        public string StationId => stationId;
        public event Action<StationInteractable> Activated;

        private void Reset()
        {
            simpleInteractable = GetComponent<XRSimpleInteractable>();
        }

        private void Awake()
        {
            if (simpleInteractable == null)
                simpleInteractable = GetComponent<XRSimpleInteractable>();

            if (stationId == "refrigerator" &&
                simpleInteractable != null &&
                simpleInteractable.colliders.Count == 0 &&
                refrigeratorDoorController != null &&
                refrigeratorDoorController.DoorPivot != null)
            {
                var doorPivot = refrigeratorDoorController.DoorPivot;
                var colliderTarget = doorPivot.childCount > 0
                    ? doorPivot.GetChild(0)
                    : doorPivot;

                var doorRenderer = colliderTarget.GetComponent<Renderer>();

                if (doorRenderer != null)
                {
                    var bounds = doorRenderer.bounds;
                    var box = colliderTarget.GetComponent<BoxCollider>();

                    if (box == null)
                        box = colliderTarget.gameObject.AddComponent<BoxCollider>();

                    box.center = colliderTarget.InverseTransformPoint(bounds.center);

                    var scale = colliderTarget.lossyScale;

                    box.size = new Vector3(
                        bounds.size.x / Mathf.Abs(scale.x),
                        bounds.size.y / Mathf.Abs(scale.y),
                        bounds.size.z / Mathf.Abs(scale.z));

                    simpleInteractable.colliders.Add(box);
                }
            }

            if (ovenProgressCanvas != null)
                ovenProgressCanvas.SetActive(false);

            if (ovenFinishCanvas != null)
                ovenFinishCanvas.SetActive(false);

            if (coffeeMachineProgressCanvas != null)
                coffeeMachineProgressCanvas.SetActive(false);

            if (coffeeMachineFinishCanvas != null)
                coffeeMachineFinishCanvas.SetActive(false);
        }

        private void Update()
        {
            if (simpleInteractable == null)
                return;

            bool isHovered = simpleInteractable.isHovered;

            // Control station prompt.
            if (promptCanvas != null)
            {
                promptCanvas.SetActive(isHovered && !isOvenBusy);
            }

            // Oven keyboard shortcut.
            if (stationId == "oven" &&
                isHovered &&
                !isOvenBusy &&
                Input.GetKeyDown(KeyCode.G))
            {
                StartOvenProcess();
            }

            // Coffee Machine keyboard shortcut.
            if (stationId == "coffee_machine" &&
                isHovered &&
                !isOvenBusy &&
                Input.GetKeyDown(KeyCode.G))
            {
                StartCoffeeMachineProcess();
            }

            // Refrigerator keyboard shortcut.
            if (stationId == "refrigerator" &&
                refrigeratorDoorController != null &&
                isHovered &&
                Input.GetKeyDown(KeyCode.G))
            {
                refrigeratorDoorController.ToggleDoor();
            }
        }

        private void OnEnable()
        {
            if (simpleInteractable == null)
                simpleInteractable = GetComponent<XRSimpleInteractable>();

            if (simpleInteractable != null)
            {
                simpleInteractable.selectEntered.AddListener(HandleSelected);
                simpleInteractable.activated.AddListener(HandleActivated);
            }
        }

        private void OnDisable()
        {
            if (simpleInteractable != null)
            {
                simpleInteractable.selectEntered.RemoveListener(HandleSelected);
                simpleInteractable.activated.RemoveListener(HandleActivated);
            }

            if (ovenCoroutine != null)
            {
                StopCoroutine(ovenCoroutine);
                ovenCoroutine = null;
            }
            if (coffeeMachineCoroutine != null)
            {
                StopCoroutine(coffeeMachineCoroutine);
                coffeeMachineCoroutine = null;
            }

            isOvenBusy = false;

            if (ovenProgressCanvas != null)
                ovenProgressCanvas.SetActive(false);

            if (ovenFinishCanvas != null)
                ovenFinishCanvas.SetActive(false);

            if (coffeeMachineProgressCanvas != null)
                coffeeMachineProgressCanvas.SetActive(false);

            if (coffeeMachineFinishCanvas != null)
                coffeeMachineFinishCanvas.SetActive(false);
        }

        private void HandleSelected(SelectEnterEventArgs args)
        {
            Activated?.Invoke(this);
        }

        private void HandleActivated(ActivateEventArgs args)
        {
            if (stationId == "oven")
            {
                StartOvenProcess();
            }

            if (stationId == "coffee_machine")
            {
                StartCoffeeMachineProcess();
            }

            if (stationId == "refrigerator" &&
                refrigeratorDoorController != null)
            {
                refrigeratorDoorController.ToggleDoor();
            }
        }

        private void PlayOperationSound(AudioClip sound)
        {
            if (operationAudioSource == null || sound == null)
                return;

            operationAudioSource.Stop();
            operationAudioSource.clip = sound;
            operationAudioSource.loop = true;
            operationAudioSource.Play();
        }

        private void StartOvenProcess()
        {
            if (isOvenBusy)
                return;

            if (ovenProgressCanvas == null ||
                ovenLoadingImage == null ||
                ovenFinishCanvas == null)
            {
                Debug.LogWarning("Oven UI references are missing.", this);
                return;
            }

            isOvenBusy = true;

            PlayOperationSound(ovenOperationSound);

            ovenCoroutine = StartCoroutine(OvenProcessRoutine());
        }

        private void StartCoffeeMachineProcess()
        {
            if (isOvenBusy)
                return;

            if (coffeeMachineProgressCanvas == null ||
                coffeeMachineLoadingImage == null ||
                coffeeMachineFinishCanvas == null)
            {
                Debug.LogWarning("Coffee Machine UI references are missing.", this);
                return;
            }

            isOvenBusy = true;

            PlayOperationSound(coffeeMachineOperationSound);

            coffeeMachineCoroutine = StartCoroutine(CoffeeMachineProcessRoutine());
        }

        private IEnumerator CoffeeMachineProcessRoutine()
        {
            // Hide prompt and show loading.
            if (promptCanvas != null)
                promptCanvas.SetActive(false);

            coffeeMachineFinishCanvas.SetActive(false);

            coffeeMachineProgressCanvas.SetActive(true);

            // Start with an empty circular progress bar.
            coffeeMachineLoadingImage.fillAmount = 0f;

            float elapsed = 0f;

            // Fill the circle from 0 to 1 in 2.5 seconds.
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / rotationDuration);

                coffeeMachineLoadingImage.fillAmount = progress;

                yield return null;
            }

            // Ensure the circle is completely filled.
            coffeeMachineLoadingImage.fillAmount = 1f;

            // Hide loading.
            coffeeMachineProgressCanvas.SetActive(false);

            // Stop operation sound and play finish sound.
            if (operationAudioSource != null)
            {
                operationAudioSource.Stop();

                operationAudioSource.loop = false;

                if (finishSound != null)
                    operationAudioSource.PlayOneShot(finishSound);
            }

            // Show Finish for 5 seconds.
            coffeeMachineFinishCanvas.SetActive(true);

            yield return new WaitForSeconds(finishDuration);

            // Hide Finish.
            coffeeMachineFinishCanvas.SetActive(false);

            // Allow the prompt to appear again if still hovered.
            isOvenBusy = false;
            coffeeMachineCoroutine = null;
        }
        private IEnumerator OvenProcessRoutine()
        {
            // Hide prompt and show loading.
            if (promptCanvas != null)
                promptCanvas.SetActive(false);

            ovenFinishCanvas.SetActive(false);
            ovenProgressCanvas.SetActive(true);

            // Start with an empty circular progress bar.
            ovenLoadingImage.fillAmount = 0f;

            float elapsed = 0f;

            // Fill the circle from 0 to 1 in 2.5 seconds.
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / rotationDuration);

                ovenLoadingImage.fillAmount = progress;

                yield return null;
            }

            // Ensure the circle is completely filled.
            ovenLoadingImage.fillAmount = 1f;

            // Hide loading.
            ovenProgressCanvas.SetActive(false);

            // Stop operation sound and play finish sound.
            if (operationAudioSource != null)
            {
                operationAudioSource.Stop();

                operationAudioSource.loop = false;

                if (finishSound != null)
                    operationAudioSource.PlayOneShot(finishSound);
            }

            // Show Finish for 5 seconds.
            ovenFinishCanvas.SetActive(true);

            yield return new WaitForSeconds(finishDuration);

            // Hide Finish.
            ovenFinishCanvas.SetActive(false);

            // Allow the prompt to appear again if still hovered.
            isOvenBusy = false;
            ovenCoroutine = null;
        }

#if UNITY_EDITOR
        public void EditorSetStationId(string value) => stationId = value;
#endif
    }
}
