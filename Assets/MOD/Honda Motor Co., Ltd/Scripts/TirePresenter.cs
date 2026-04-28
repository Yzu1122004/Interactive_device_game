using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Honda
{
    public class TirePresenter : MonoBehaviour
    {
        [Tooltip( "直径" )]
        [SerializeField]
        private float _diameter;

        public void AddDistance( float distance )
        {
            var outerCircumference = _diameter * Mathf.PI;
            var round = distance / outerCircumference;
            var angle = 360.0f * round;

            transform.Rotate( Vector3.right * angle );
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
        }
    }
}
