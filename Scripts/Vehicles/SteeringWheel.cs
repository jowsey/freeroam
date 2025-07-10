using UnityEngine;

namespace Vehicles
{
    public enum RotateAxis
    {
        X,
        Y,
        Z
    }

    public class SteeringWheel : MonoBehaviour
    {
        private VehicleInput _vehicleInput;

        public RotateAxis RotateAxis;

        public bool Invert;

        private void Start() => _vehicleInput = GetComponentInParent<VehicleInput>();

        private void Update()
        {
            var amount = _vehicleInput.SteerInput * (45 * (Invert ? -1 : 1));
            var eulerAngles = transform.localEulerAngles;
    
            transform.localEulerAngles = RotateAxis switch
            {
                RotateAxis.X => new Vector3(amount, eulerAngles.y, eulerAngles.z),
                RotateAxis.Y => new Vector3(eulerAngles.x, amount, eulerAngles.z),
                RotateAxis.Z => new Vector3(eulerAngles.x, eulerAngles.y, amount),
                _ => transform.localEulerAngles
            };
        }
    }
}
