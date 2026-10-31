namespace Chess.Core.Services;

public interface ICommand
{
    bool Execute();
    bool Undo();
}
