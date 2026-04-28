using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Events;

namespace Honda
{
    public class WinkerDirectorPlayer : MonoBehaviour
    {
        [SerializeField]
        private PlayableDirector _directorWinkerLeft;
        [SerializeField]
        private PlayableDirector _directorWinkerRight;
        [SerializeField]
        private UnityEvent OnWinkerLeftOff;
        [SerializeField]
        private UnityEvent OnWinkerRightOff;


        public void OnChanged_Winker( bool state )
        {
            OnChanged_WinkerLeft( state );
            OnChanged_WinkerRight( state );
        }

        public void OnChanged_WinkerLeft( bool state )
        {
            if ( state )
            {
                _directorWinkerLeft?.Play();
            }
            else
            {
                _directorWinkerLeft?.Stop();
                OnWinkerLeftOff?.Invoke();
            }
        }

        public void OnChanged_WinkerRight( bool state )
        {
            if ( state )
            {
                _directorWinkerRight?.Play();
            }
            else
            {
                _directorWinkerRight?.Stop();
                OnWinkerRightOff?.Invoke();
            }
        }
    }
}
