using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Displays a floating damage number at a specified world position.
    /// The text floats upward and fades out over time, then destroys itself.
    /// </summary>
    public class DamagePopup : MonoBehaviour, IPoolR
    {
        [SerializeField] private PoolRuntimeSO poolRuntimeSo;
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float duration = 1f;

        private TextMesh _textMesh;
        private float _elapsed;
        private Color _originalColor;
        
        /// <summary>
        /// Instantiates a damage popup at the given position showing the specified damage amount.
        /// </summary>
        /// <param name="position">World position for the popup.</param>
        /// <param name="damage">Damage value to display.</param>
        public void Show(Vector3 position, int damage)
        {
            this.transform.position = position;
            _textMesh.color = _originalColor;
            Initialize(damage);
        }

        private void Awake()
        {
            _textMesh = gameObject.GetComponent<TextMesh>();
            GetComponent<MeshRenderer>().sortingOrder = short.MaxValue;
            _originalColor = _textMesh.color;
        }

        private void Initialize(int damage)
        {
            _textMesh.text = damage.ToString();
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            transform.position += Vector3.up * (moveSpeed * delta);
            _elapsed += delta;
            float t = _elapsed / duration;
            if (t >= 1f)
            {
                poolRuntimeSo.Return(gameObject);
            }
            else
            {
                Color c = _originalColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                _textMesh.color = c;
            }
        }

        public string Key { get; set; }
        
        public void Request()
        {
            
        }

        public void Return()
        {
            _elapsed = 0;
        }
    }

}
