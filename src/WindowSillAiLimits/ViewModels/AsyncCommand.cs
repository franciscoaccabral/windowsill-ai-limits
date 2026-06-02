using System.Windows.Input;

namespace WindowSillAiLimits.ViewModels;

public sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    private int _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => Volatile.Read(ref _isExecuting) == 0;

    public async void Execute(object? parameter)
    {
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
        {
            return;
        }

        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await execute();
        }
        finally
        {
            Volatile.Write(ref _isExecuting, 0);
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
