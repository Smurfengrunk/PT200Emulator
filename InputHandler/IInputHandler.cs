namespace InputHandler
{
    public interface IInputHandler
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}