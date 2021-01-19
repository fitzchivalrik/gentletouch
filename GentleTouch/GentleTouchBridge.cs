using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace GentleTouch
{
    public class GentleTouchBridge : IDalamudPlugin
    {
        
        public string Name => Constant.PluginName;
        private GentleTouch _plugin = null!;
        
        // TODO TESTING
        private readonly CancellationTokenSource _source = new();
        private CancellationToken _token;

        private Task _task;
        // TODO END TESTING

        public void Initialize(DalamudPluginInterface pi)
        {
            var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _plugin = new GentleTouch(pi, config);
            // TODO TESTING
            _token = _source.Token;
            _task = Task.Run(async () =>
            {
                while (true)
                {
                    for (var i = 0; i < 1_000_000; i++)
                    {
                        var u = i - 1;
                        i = u + 1;
                    }
                    await Task.Delay(1000);
                    if (_token.IsCancellationRequested)
                    {
                        PluginLog.Error("Cancellation was requested!");
                        PluginLog.Warning($"T Task Cancelled? {_task.IsCanceled}");
                        PluginLog.Warning($"T Task Completed? {_task.IsCompleted}");
                        //_token.ThrowIfCancellationRequested();
                        break;
                    }
                }
            }, _token);
                
            // TODO END TESTING
            
            
        }

        public void Dispose()
        {
            // TODO TESTING
            _source.Cancel();
            try
            {
                PluginLog.Warning($"Task Cancelled? {_task.IsCanceled}");
                PluginLog.Warning($"Task Completed? {_task.IsCompleted}");
                while(!_task.IsCompleted) {}
            }
            catch (Exception e)
            {
                PluginLog.Warning($"E Task Cancelled? {_task.IsCanceled}");
                PluginLog.Warning($"E Task Completed? {_task.IsCompleted}");
                PluginLog.Error($"Task Cancelled with: {e}");
            }
            finally
            {
                PluginLog.Warning($"F Task Cancelled? {_task.IsCanceled}");
                PluginLog.Warning($"F Task Completed? {_task.IsCompleted}");
                PluginLog.Error($"Disposing CancellationSource.");
                //_task.Dispose();
                _source.Dispose();                
            }
            // TODO END TESTING
            _plugin.Dispose();
        }
    }
}