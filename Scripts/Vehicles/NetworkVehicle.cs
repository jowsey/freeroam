using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core;
using EVP;
using Mirror;
using Player;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Random = UnityEngine.Random;

namespace Vehicles
{
    public enum VehicleType
    {
        Nekomata,
        Truck,
        APC
    }

    public static class VehicleSkins
    {
        public static readonly List<Material> Skins;

        static VehicleSkins()
        {
            // load all addressable skins
            Skins = Addressables.LoadAssetsAsync<Material>("VehicleSkin", null)
                .WaitForCompletion()
                .OrderBy(x => x.name)
                .ToList();
        }
    }

    public class NetworkVehicle : NetworkBehaviour
    {
        private VehicleController _vehicleController;
        private VehicleInput _vehicleInput;

        public VehicleType Type;

        public List<VehicleSeat> Seats;

        public Transform LeftPedal;
        public Transform RightPedal;

        public Transform LeftWheelHandle;
        public Transform RightWheelHandle;

        public List<ModificationCategory> ModificationCategories;

        public IEnumerable<VehicleModData> ActiveModifications => ModificationCategories
            .Select(ctgry => ctgry.SelectedIndex == -1 ? null : ctgry.Attachments[ctgry.SelectedIndex].GetComponent<VehicleModData>())
            .Where(attachment => attachment != null).ToArray();

        [SyncVar(hook = nameof(OnSkinChanged))]
        public int SkinIndex;

        [Command]
        public void CmdSetSkinIndex(int index)
        {
            index = Mathf.Clamp(index, 0, VehicleSkins.Skins.Count - 1);

            if(isServer) OnSkinChanged(SkinIndex, index);
            SkinIndex = index;
        }

        public void OnSkinChanged(int oldIndex, int newIndex)
        {
            if(oldIndex == newIndex) return;

            foreach (var rndr in GetComponentsInChildren<Renderer>())
            {
                // Debug.Log(rndr.name + " " + rndr.material.name.Replace(" (Instance)", ""));
                if(!VehicleSkins.Skins.Select(skin => skin.name).Contains(rndr.material.name.Replace(" (Instance)", ""))) continue;

                rndr.material = VehicleSkins.Skins[newIndex];
            }
        }

        public readonly SyncList<int> ModificationIndexes = new();

        [Command]
        public void CmdSetAttachmentCategoryIndex(int categoryIndex, int attachmentIndex)
        {
            // if category index is more than number of categories, return
            if(categoryIndex >= ModificationCategories.Count) return;

            // if category index is higher than length of attachment indexes, fill with -1 to match length
            if(categoryIndex >= ModificationIndexes.Count)
            {
                ModificationIndexes.AddRange(Enumerable.Repeat(-1, categoryIndex - ModificationIndexes.Count + 1));
            }

            // if attachment index is more than number of attachments in category, return
            if(attachmentIndex >= ModificationCategories[categoryIndex].Attachments.Count) return;

            ModificationIndexes[categoryIndex] = attachmentIndex;
        }

        public void OnModificationIndexesChanged(SyncList<int>.Operation op, int index, int oldIndex, int newIndex)
        {
            if(oldIndex != newIndex) ApplyModifications();
        }

        private void ApplyModifications()
        {
            var indexes = ModificationIndexes.ToList();

            // fill indexes with -1 if not enough
            if(indexes.Count < ModificationCategories.Count) indexes = indexes.Concat(Enumerable.Repeat(-1, ModificationCategories.Count - indexes.Count)).ToList();

            for (var i = 0; i < ModificationCategories.Count; i++)
            {
                var category = ModificationCategories[i];

                if(category.SelectedIndex == indexes[i]) continue;
                category.SelectedIndex = indexes[i];

                // disable all attachments
                foreach (var attachment in category.Attachments) attachment.SetActive(false);

                // enable selected attachment
                if(indexes[i] == -1)
                {
                    if(category.UseFirstAsDefault) category.Attachments[0].SetActive(true);
                }
                else
                {
                    category.Attachments[indexes[i]].SetActive(true);
                }
            }

            OnSkinChanged(-1, SkinIndex);
        }

        [Command(requiresAuthority = false)]
        public async void CmdEnter(NetworkConnectionToClient sender = null)
        {
            var player = sender!.identity.GetComponent<PlayerController>();

            // Exit vehicle if player already in it
            var playerSeat = Seats.FirstOrDefault(s => s.TakenBy == player);
            if(playerSeat != null)
            {
                playerSeat.TakenBy = null;
                
                player.CurrentSeat = -1;
                player.CurrentVehicle = null;
                await Task.Yield();
            }

            var availableSeat = Seats.FirstOrDefault(s => s.TakenBy == null);
            if(availableSeat == null) return;
            
            availableSeat.TakenBy = player;
            if(availableSeat.IsDriverSeat) netIdentity.AssignClientAuthority(sender);

            player.CurrentSeat = Seats.IndexOf(availableSeat);
            player.CurrentVehicle = netIdentity;
        }

        [Command(requiresAuthority = false)]
        public void CmdLeave(NetworkConnectionToClient sender = null)
        {
            var player = sender!.identity.GetComponent<PlayerController>();
            var seat = Seats.Find(s => s.TakenBy == player);
            if(seat == null) return;

            seat.TakenBy = null;

            player.CurrentVehicle = null;
            player.CurrentSeat = -1;

            if(seat.IsDriverSeat)
            {
                _vehicleInput.ThrottleInput = 0;
                _vehicleInput.SteerInput = 0;
                _vehicleInput.HandbrakeInput = 0;

                netIdentity.RemoveClientAuthority();
            }
        }

        private void Awake()
        {
            ModificationIndexes.Callback += OnModificationIndexesChanged;
        }

        private void Start()
        {
            _vehicleController = GetComponent<VehicleController>();
            _vehicleInput = GetComponent<VehicleInput>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // synclists use different sync hook system so we manually call on start
            ApplyModifications();

            GetComponent<VehicleAudio>().enabled = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // SkinIndex = VehicleSkins.Skins.Select(s => s.name).ToList().IndexOf(GetComponentInChildren<Renderer>().material.name.Replace(" (Instance)", ""));
            SkinIndex = Random.Range(0, 12);
            OnSkinChanged(-1, SkinIndex);
        }

        private void Update()
        {
            _vehicleController.throttleInput = _vehicleInput.ThrottleInput;
            _vehicleController.steerInput = _vehicleInput.SteerInput;
            _vehicleController.handbrakeInput = _vehicleInput.HandbrakeInput;
        }

        public static string VehicleTypeToName(VehicleType type)
        {
            return type switch
            {
                VehicleType.Nekomata => "Nekomata",
                VehicleType.Truck => "Truck",
                VehicleType.APC => "APC",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
