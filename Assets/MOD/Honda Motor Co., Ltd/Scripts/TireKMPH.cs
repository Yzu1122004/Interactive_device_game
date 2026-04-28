using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Honda
{
    public class TireKMPH : MonoBehaviour
    {
        [SerializeField]
        private Text _textValue;

        private float _speed = 0f;
        public float KmPerHour => _speed;

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
            UpdateText();
        }

        public void AddSpeed( float value )
        {
            _speed += value;
        }

        private void UpdateText()
        {
            if ( _textValue )
            {
                _textValue.text = _speed.ToString( "F3" );
            }
        }
    }
}
