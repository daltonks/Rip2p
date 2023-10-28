using Rip2p.Session.Data;
using UnityEngine;
using Util;
using INetworkData = Rip2p.Session.Data.INetworkData;

namespace Rip2p.Session.Syncs
{
    public abstract class InterpolatedNetworkSync<TData> : NetworkSync<TData> 
        where TData : class, INetworkData, new()
    {
        public override bool SendDataOnTick => true;
        
        public int NumFixedUpdatesBetweenReceivingData { get; set; }
            = NetworkSession.FixedUpdatesBetweenTicks;
        protected float TimeBetweenReceivingData 
            => Time.fixedDeltaTime * NumFixedUpdatesBetweenReceivingData;

        private int _interpolationFrame;
        private float _interpolationProportion;
        private TData _interpolationStartData;

        private bool _receivedInitialData;
        private readonly CircularBuffer<(Data.NetworkDataWrapper NetworkData, TData Data)> _buffer = new(capacity: 2);

        protected override void OnReceivedData(NetworkDataWrapper networkData, TData data)
        {
            if (IsOwned)
            {
                return;
            }
            
            networkData.AddUsage();
            
            if (!_receivedInitialData)
            {
                OnStartInterpolatingToNextData(data, data);
                Interpolate(data, data, 1);

                _interpolationStartData = data;

                for (var i = 0; i < _buffer.Size; i++)
                {
                    var networkDataClone = NetworkDataWrapper.GetFromCache(typeof(TData));
                    var dataClone = (TData)networkDataClone.Value;
                    UpdateData(dataClone);
                    _buffer.PushBack((networkDataClone, dataClone));
                }

                _receivedInitialData = true;

                return;
            }

            if (_buffer.IsFull)
            {
                StartInterpolatingToNextData();
            }

            _buffer.PushBack((networkData, data));
        }

        protected virtual void FixedUpdate()
        {
            if (IsOwned)
            {
                return;
            }

            if (_buffer.IsEmpty)
            {
                return;
            }
            
            _interpolationFrame++;
            const float proportionScaleToPreventTimeDrift = 1.05f;
            _interpolationProportion = _interpolationFrame / (NumFixedUpdatesBetweenReceivingData * proportionScaleToPreventTimeDrift);
        }

        protected virtual void Update()
        {
            if (IsOwned)
            {
                return;
            }

            if (_buffer.IsEmpty)
            {
                return;
            }
            
            Interpolate(
                _interpolationStartData, 
                _buffer[0].Data, 
                _interpolationProportion);

            if (_interpolationFrame >= NumFixedUpdatesBetweenReceivingData && _buffer.Size > 1)
            {
                StartInterpolatingToNextData();
            }
        }
        
        private void StartInterpolatingToNextData()
        {
            var previousData = _buffer[0];

            _interpolationFrame = 0;
            _interpolationProportion = 0;
            _buffer.PopFront();

            UpdateData(_interpolationStartData);
            OnStartInterpolatingToNextData(previousData.Data, _buffer[0].Data);

            previousData.NetworkData.RemoveUsage();
        }
        
        protected abstract void OnStartInterpolatingToNextData(
            TData previousData,
            TData nextData);
        
        protected abstract void Interpolate(
            TData startData,
            TData currentData,
            float interpolationProportion);
    }
}