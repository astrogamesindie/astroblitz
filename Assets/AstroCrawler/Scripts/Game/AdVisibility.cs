using UnityEngine;
using UnityEngine.Events;

namespace AstroCrawler
{
    /// <summary>Attach to an always-active ad host; content must be a separate child.</summary>
    public sealed class AdVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [Tooltip("Wire the ad SDK's hide/destroy/cancel calls here, including pending callbacks.")]
        [SerializeField] private UnityEvent onAdsBlocked = new UnityEvent();
        private AndroidPurchases store;
        private bool blocked;

        private void OnEnable()
        {
            store = AndroidPurchases.EnsureCreated();
            if (store != null) store.Changed += Refresh;
            blocked = false;
            Refresh();
        }

        private void Refresh()
        {
            var allow = store != null && !store.AdsRemoved;
            if (content != null && content != gameObject && content.activeSelf != allow) content.SetActive(allow);
            if (!allow && !blocked) onAdsBlocked.Invoke();
            blocked = !allow;
        }

        private void OnDisable()
        {
            if (store != null) store.Changed -= Refresh;
            store = null;
        }
    }
}
