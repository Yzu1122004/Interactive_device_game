using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Honda
{
    public class TireRotateScene : MonoBehaviour
    {
        [SerializeField]
        private List<TirePresenter> _tirePresenters;
        [SerializeField]
        private TireKMPH _tireKMPH;

        [SerializeField]
        private float _addDistance = 0.5f;

        private float _perHour;

        [SerializeField]
        private float _mps = 0f;

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
            UpdateDistance();
        }

        public void OnClick_AddDistance()
        {
            foreach ( var presenter in _tirePresenters )
            {
                presenter?.AddDistance( _addDistance );
            }
        }

        private void UpdateDistance()
        {
            if ( _tireKMPH )
            {
                var kmph = _tireKMPH.KmPerHour;
                var mph = 1000.0f * kmph;
                var mpm = mph / 60.0f;
                var mps = mpm / 60.0f;
                _mps = mps;

                var distance = mps * Time.deltaTime;

                foreach ( var presenter in _tirePresenters )
                {
                    presenter?.AddDistance( distance );
                }
            }
        }
    }
}
