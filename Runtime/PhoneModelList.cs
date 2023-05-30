using UnityEngine;

namespace HoloInteractive.XR.HoloKit
{
    [CreateAssetMenu(menuName = "HoloKit/PhoneModelList")]
    public class PhoneModelList : ScriptableObject
    {
        public PhoneModel[] PhoneModels;
    }
}
