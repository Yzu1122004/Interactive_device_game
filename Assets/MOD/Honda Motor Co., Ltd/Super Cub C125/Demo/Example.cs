using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Honda.SuperCub.Demo
{
    public class Example : MonoBehaviour
    {
        [SerializeField]
        private List<Animator> _animators;

        private static readonly string kLayerName_FrontDamper = "front damper";
        private static readonly string kLayerName_RearDamper = "rear damper";
        private static readonly string kLayerName_HandleLeft = "handle left";
        private static readonly string kLayerName_HandleRight = "handle right";
        private static readonly string kLayerName_SideStand = "side stand";
        private static readonly string kLayerName_FrontBrake = "front brake";
        private static readonly string kLayerName_RearWheelBrake = "rear wheel brake";
        private static readonly string kLayerName_GearChangePedalNega = "gear change pedal negative";
        private static readonly string kLayerName_GearChangePedalPosi = "gear change pedal positive";
        private static readonly string kLayerName_SwingArm = "swing arm";

        private static readonly string kParameterName_TailLamp = "Tail Lamp";
        private static readonly string kParameterName_HeadLight = "Head Light";
        private static readonly string kParameterName_NumberPlateLight = "Number Plate Light";
        private static readonly string kParameterName_FrontWinkerLeft = "Front Winker Left";
        private static readonly string kParameterName_FrontWinkerRight = "Front Winker Right";
        private static readonly string kParameterName_RearWinkerLeft = "Rear Winker Left";
        private static readonly string kParameterName_RearWinkerRight = "Rear Winker Right";
        private static readonly string kParameterName_BrakeLamp = "Brake Lamp";


        // Start is called before the first frame update
        void Start()
        {
        }

        public void OnChanged_TailLamp( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_TailLamp, state );
            }
        }

        public void OnChanged_HeadLight( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_HeadLight, state );
            }
        }

        public void OnChanged_Winker( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_FrontWinkerLeft, state );
                animator?.SetBool( kParameterName_FrontWinkerRight, state );

                animator?.SetBool( kParameterName_RearWinkerLeft, state );
                animator?.SetBool( kParameterName_RearWinkerRight, state );
            }
        }

        public void OnChanged_WinkerLeft( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_FrontWinkerLeft, state );
                animator?.SetBool( kParameterName_RearWinkerLeft, state );
            }
        }

        public void OnChanged_WinkerRight( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_FrontWinkerRight, state );
                animator?.SetBool( kParameterName_RearWinkerRight, state );
            }
        }

        public void OnChanged_NumberLight( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_NumberPlateLight, state );
            }
        }

        public void OnChanged_BrakeLamp( bool state )
        {
            foreach ( var animator in _animators )
            {
                animator?.SetBool( kParameterName_BrakeLamp, state );
            }
        }

        public void OnChanged_SpeedMeter( float speed )
        {
        }

        public void OnChanged_FrontDamper( float rate )
        {
            SetLayerWeight( kLayerName_FrontDamper, rate );
        }

        public void OnChanged_RearDamper( float rate )
        {
            SetLayerWeight( kLayerName_RearDamper, rate );
        }

        public void OnChanged_SwingArm( float rate )
        {
            SetLayerWeight( kLayerName_SwingArm, rate );
        }

        public void OnChanged_FrontBreak( float rate )
        {
            SetLayerWeight( kLayerName_FrontBrake, rate );
        }

        public void OnChanged_RearWheelBreak( float rate )
        {
            SetLayerWeight( kLayerName_RearWheelBrake, rate );
        }

        public void OnChanged_GearPedal( float rate )
        {
            rate = (rate * 2.0f) - 1.0f; // -1.0f 〜 1.0f

            foreach ( var animator in _animators )
            {
                var layerIndexNega = animator?.GetLayerIndex( kLayerName_GearChangePedalNega ) ?? 0;
                var layerIndexPosi = animator?.GetLayerIndex( kLayerName_GearChangePedalPosi ) ?? 0;

                animator?.SetLayerWeight( layerIndexNega, Mathf.Clamp( rate, -1f, 0f ) * -1f );
                animator?.SetLayerWeight( layerIndexPosi, Mathf.Clamp01( rate ) );
            }
        }

        public void OnChanged_Handle( float rate )
        {
            rate = (rate * 2.0f) - 1.0f; // -1.0f 〜 1.0f

            foreach ( var animator in _animators )
            {
                var layerIndexL = animator?.GetLayerIndex( kLayerName_HandleLeft ) ?? 0;
                var layerIndexR = animator?.GetLayerIndex( kLayerName_HandleRight ) ?? 0;

                animator?.SetLayerWeight( layerIndexL, Mathf.Clamp( rate, -1f, 0f ) * -1f );
                animator?.SetLayerWeight( layerIndexR, Mathf.Clamp01( rate ) );
            }
        }

        public void OnChanged_SideStand( float rate )
        {
            SetLayerWeight( kLayerName_SideStand, rate );
        }


        private Animator GetAanyAnaimator()
        {
            return _animators?.First();
        }

        private int GetLayerIndex( string layerName )
        {
            return GetAanyAnaimator()?.GetLayerIndex( layerName ) ?? -1;
        }

        private bool SetLayerWeight( string layerName, float rate )
        {
            var layerIndex = GetLayerIndex( layerName );
            if ( 0 <= layerIndex )
            {
                foreach ( var animator in _animators )
                {
                    animator?.SetLayerWeight( layerIndex, Mathf.Clamp01( rate ) );
                }
            }

            return 0 <= layerIndex;
        }
    }
}
