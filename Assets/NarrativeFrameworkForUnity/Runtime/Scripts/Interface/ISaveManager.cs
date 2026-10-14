using System;

namespace SimulationGame.Interfaces
{
    public interface ISaveManager
    {
        bool HasSave(string saveName);
        void SaveGame(ISaveDataProvider provider);
        void LoadGame(ISaveDataReceiver receiver);
        void DeleteSave(string saveName);
    }
}
