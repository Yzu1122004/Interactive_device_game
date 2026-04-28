using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Honda
{
    public class AutoRotation : MonoBehaviour
    {
        [SerializeField]
        private Transform _target;
        [SerializeField]
        private float _rotateDegreeSpeed = 180f;

        public void SetRotateDegreeSpeed( float speed )
        {
            _rotateDegreeSpeed = speed;
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        [System.Obsolete]
        void Update()
        {
            if ( _target )
            {
                var eulerAngles = Vector3.zero;
                eulerAngles.y = _rotateDegreeSpeed * Time.deltaTime;
                _target.Rotate( eulerAngles );
            }
        }
    }
}
