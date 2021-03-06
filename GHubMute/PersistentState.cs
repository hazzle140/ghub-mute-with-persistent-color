using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace GHubMute
{
    public class PersistentState : IDisposable
    {
        public static async Task<PersistentState> LoadAsync()
        {
            var currentDirectory = Path.GetDirectoryName((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location);
            var statePath = Path.Combine(currentDirectory, "ghub-mute-state.json");

            var stream = new FileStream(statePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.SequentialScan);
            var state = new PersistentState(stream);
            await state.LoadInternalAsync();
            return state;
        }

        private PersistentState(FileStream stream)
        {
            _stream = stream;
        }

        private FileStream _stream;
        private bool _requiresSave = false;
        private SerializedState _state;

        public async ValueTask SaveChangesAsync()
        {
            if (!_requiresSave)
            {
                return;
            }

            _stream.Seek(0, SeekOrigin.Begin);
            await JsonSerializer.SerializeAsync(_stream, _state).ConfigureAwait(false);
            _stream.SetLength(_stream.Position);
            await _stream.FlushAsync();
        }

        public void Dispose() => _stream.Dispose();

        public bool DeviceIdIsManaged(string id) => _state.ManagedDeviceIds.Contains(id);

        public void FlagDeviceAsManaged(string id)
        {
            if (!DeviceIdIsManaged(id))
            {
                _state.ManagedDeviceIds.Add(id);
                _requiresSave = true;
            }
        }

        private async ValueTask LoadInternalAsync()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            try
            {
                _state = (await JsonSerializer.DeserializeAsync<SerializedState>(_stream)) ?? new SerializedState();
            }
            catch
            {
                _state = new SerializedState();
            }

            _state.ManagedDeviceIds ??= new List<string>();
        }

        private class SerializedState
        {
            public List<string> ManagedDeviceIds { get; set; }
        }
    }
}
