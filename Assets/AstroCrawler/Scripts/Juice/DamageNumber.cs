using System;
using TMPro;
using UnityEngine;

namespace AstroCrawler
{
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private MeshRenderer meshRenderer;

        public void Show(Vector3 position, string value, Color color, float size, Action<DamageNumber> finished)
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (label == null) return;

            // The prefab is authored without a font (the SDF asset may not
            // exist yet when prefabs are generated), and a TextMeshPro with no
            // font renders nothing and warns every frame.
            if (label.font == null) label.font = UIFactory.TmpFont;

            LeanTween.cancel(gameObject);
            transform.position = position;
            transform.localScale = Vector3.one * size;
            label.text = value;
            label.color = color;
            if (meshRenderer != null) meshRenderer.sortingOrder = 60;
            gameObject.SetActive(true);

            const float lifetime = 0.75f;
            LeanTween.move(gameObject, position + Vector3.up * (0.8f * size), lifetime)
                .setEase(LeanTweenType.easeOutCubic).setIgnoreTimeScale(true);
            LeanTween.value(gameObject, alpha =>
            {
                if (label == null) return;
                var current = label.color;
                current.a = color.a * alpha;
                label.color = current;
            }, 1f, 0f, lifetime).setIgnoreTimeScale(true)
                .setOnComplete(() => finished?.Invoke(this));
        }

        public void Hide()
        {
            LeanTween.cancel(gameObject);
            gameObject.SetActive(false);
        }
    }
}
