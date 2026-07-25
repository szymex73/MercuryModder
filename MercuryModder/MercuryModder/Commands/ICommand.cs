using System.CommandLine;

namespace MercuryModder.Commands;

public interface ICommand
{
    Command Build();    
}