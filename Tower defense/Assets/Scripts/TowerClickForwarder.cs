using UnityEngine;

// Encaminha o clique (OnMouseDown) para a TowerBase quando o Collider2D está num
// GameObject diferente do script da torre (caso do Sniper: collider na raiz, script num filho).
[RequireComponent(typeof(Collider2D))]
public class TowerClickForwarder : MonoBehaviour
{
    private void OnMouseDown()
    {
        TowerBase tower = GetComponentInChildren<TowerBase>();
        if (tower != null) tower.ToggleUpgradeUI();
    }
}
