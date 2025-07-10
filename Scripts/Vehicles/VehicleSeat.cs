using Player;
using UnityEngine;

namespace Vehicles
{
    public enum SeatSide
    {
        Left,
        Right
    }
    
    public class VehicleSeat : MonoBehaviour
    {
        public bool IsDriverSeat;
        public SeatSide Side;

        public PlayerController TakenBy;
    }
}
