public interface IUpgradable
{
    int GetCurrentLevel();
    int GetMaxLevel();
    int CalculateNextCost();
    void OpenUpgradeUI();    // ← Adicione este
    void CloseUpgradeUI();   // ← Adicione este
    void Upgrade();

    // Opcional, se quiser mostrar descrição ou outros stats
    string GetUpgradeDescription();
}