using System.Collections.Generic;
using UnityEngine;

// Bomba — VERBO: dano em massa. Uma trilha transforma a explosão em CLUSTER (a explosão gera outras
// ao redor); a outra deixa FOGO no chão, que queima quem atravessar. É a resposta do jogo a enxames,
// e a única fonte de dano que não precisa acertar cada inimigo individualmente.
public class AoETurret : TowerBase, ITargeting
{
    [Header("Bomba - referências")]
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Bomba - atributos base")]
    [SerializeField] private float aps = 0.8f;
    [SerializeField] private float baseExplosionDamage = 40f;
    [SerializeField] private float baseExplosionRadius = 2.5f;

    [Header("Mira")]
    [SerializeField] private TargetingPriority targetPriority = TargetingPriority.First;

    private Transform target;

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Carga: a explosão se multiplica
        a.Add(new UpgradeTier("Carga Reforçada", "Explosões mais fortes.", 150, damageMult: 1.6f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("Estilhaços", "Muito mais dano em área.", 300, damageMult: 1.7f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("BOMBA CACHO", "Cada explosão dispara 4 explosões secundárias em volta.",
            600, damageMult: 1.8f, scaleMult: 1.2f, ability: "cluster", tint: new Color(1f, 0.5f, 0.2f)));

        // Trilha B — Incêndio: deixa dano no terreno
        b.Add(new UpgradeTier("Pavio Curto", "Lança bombas mais rápido.", 130, rateMult: 1.5f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Napalm", "As explosões deixam FOGO no chão, que queima quem passar.",
            320, rangeMult: 1.3f, scaleMult: 1.1f, ability: "napalm"));
        b.Add(new UpgradeTier("MAR DE CHAMAS", "Fogo muito maior, mais duradouro e detecção de camuflados.",
            620, rangeMult: 1.3f, rateMult: 1.2f, grantsCamo: true, scaleMult: 1.15f, ability: "firestorm",
            tint: new Color(1f, 0.35f, 0.1f)));
    }

    private bool LeavesFire => HasAbility("napalm") || HasAbility("firestorm");
    private bool Clusters => HasAbility("cluster");

    protected override void Tick()
    {
        if (target == null || IsoGrid.CellDistance(target.position, transform.position) > targetingRange)
            target = AcquireTarget(targetPriority);
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, aps * RateMult);

    protected override bool TryFire()
    {
        if (target == null || bulletPrefab == null || firingPoint == null) return false;

        GameObject obj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
        IsoSorter.Attach(obj, moves: true); // prefab vem com sortingOrder fixo (1) — some atrás do tabuleiro sem isto
        AoEBullet bullet = obj.GetComponent<AoEBullet>();
        if (bullet != null)
        {
            bullet.SetTarget(target);
            bullet.SetDamage(CurrentDamage());
            bullet.SetRadius(CurrentRadius());
            bullet.SetCanSeeCamo(SeesCamo);

            if (Clusters) bullet.SetCluster(4);
            if (LeavesFire)
            {
                bool storm = HasAbility("firestorm");
                bullet.SetFire(
                    CurrentDamage() * (storm ? 0.25f : 0.15f),
                    CurrentRadius() * (storm ? 1.1f : 0.7f),
                    storm ? 5f : 3f);
            }
        }
        return true;
    }

    private float CurrentDamage() => baseExplosionDamage * DamageMult;
    private float CurrentRadius() => baseExplosionRadius * Mathf.Pow(DamageMult, 0.35f); // bombas maiores explodem mais

    public override string GetStatsText()
    {
        string extra = "";
        if (Clusters) extra += "  ·  4 explosões secundárias";
        if (LeavesFire) extra += HasAbility("firestorm") ? "  ·  MAR DE CHAMAS (5s)" : "  ·  deixa fogo (3s)";
        return $"Dano {CurrentDamage():0}  ·  Raio {CurrentRadius():0.0}  ·  Alcance {targetingRange:0.0}  ·  {(aps * RateMult):0.0}/s{extra}";
    }

    public void CycleTargeting() => targetPriority = Targeting.Next(targetPriority);
    public string GetTargetingLabel() => Targeting.Label(targetPriority);
}
