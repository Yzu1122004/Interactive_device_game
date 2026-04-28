using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Honda
{
    public class WinkerPresenter : MonoBehaviour
    {
        [SerializeField]
        private UnityEvent<bool> OnWinkerLeft;
        [SerializeField]
        private UnityEvent<bool> OnWinkerRight;

        public void RecieveWinkerLeftOn()
        {
            Debug.Log( $"RecieveWinkerLeftOn ({Time.frameCount})" );
            OnWinkerLeft?.Invoke( true );
        }

        public void RecieveWinkerLeftOff()
        {
            Debug.Log( $"RecieveWinkerLeftOff ({Time.frameCount})" );
            OnWinkerLeft?.Invoke( false );
        }

        public void RecieveWinkerRightOn()
        {
            Debug.Log( $"RecieveWinkerRightOn ({Time.frameCount})" );
            OnWinkerRight?.Invoke( true );
        }

        public void RecieveWinkerRightOff()
        {
            Debug.Log( $"RecieveWinkerRightOff ({Time.frameCount})" );
            OnWinkerRight?.Invoke( false );
        }
    }
}
