using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="AuditLogViewModel"/> (Phase 6, tâche 4 ; filtres et
/// pagination chantier P4) — <see cref="IAuditLogRepository"/> mocké ;
/// <see cref="ListerAuditLog"/> et le ViewModel sont réels.
/// </summary>
public class AuditLogViewModelTests
{
    private static readonly EntreeAuditLog Entree = new(
        1, "2026-07-17T10:00:00Z", "admin", AuditActions.AppliquerEvolution, AuditEntityTypes.ValeurPoint, "VP-2026-01-01", null, null);

    [Fact]
    public async Task ChargerAsync_succes_peuple_les_entrees()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<FiltreAuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<EntreeAuditLog>>([Entree]));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Single(vm.Entrees);
        Assert.Equal(AuditActions.AppliquerEvolution, vm.Entrees[0].Action);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAsync_echec_du_repository_affiche_l_erreur()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<FiltreAuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<EntreeAuditLog>>(Error.Failure("Panne base de données.")));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Empty(vm.Entrees);
        // Times.AtLeastOnce (pas Once) : le constructeur invoque déjà
        // ChargerCommand en fire-and-forget (patron établi, cf. sélecteurs
        // référentiels) — cet appel explicite s'ajoute au premier, non
        // déterministe quant au nombre exact d'invocations en cas d'échec.
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("Panne"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ChargerAsync_transmet_les_filtres_saisis_au_use_case()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        FiltreAuditLog? filtreRecu = null;
        auditLog.Setup(a => a.ListerAsync(It.IsAny<FiltreAuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<FiltreAuditLog, CancellationToken>((f, _) => filtreRecu = f)
            .ReturnsAsync(Result.Success<IReadOnlyList<EntreeAuditLog>>([]));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object)
        {
            FiltreActeur = "  admin  ",
            FiltreAction = AuditActions.Calcul,
            FiltreTypeEntite = AuditEntityTypes.Bulletin,
            FiltreDateDebut = "2026-01-01",
            FiltreDateFin = "2026-12-31",
        };
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.NotNull(filtreRecu);
        Assert.Equal("admin", filtreRecu!.Actor); // trim appliqué
        Assert.Equal(AuditActions.Calcul, filtreRecu.Action);
        Assert.Equal(AuditEntityTypes.Bulletin, filtreRecu.EntityType);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), filtreRecu.DateDebut);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), filtreRecu.DateFin);
        Assert.Equal(1, filtreRecu.Page);
    }

    [Fact]
    public async Task ChargerAsync_date_invalide_affiche_l_erreur()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<FiltreAuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<EntreeAuditLog>>([]));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object)
        {
            FiltreDateDebut = "pas-une-date",
        };

        await vm.ChargerCommand.ExecuteAsync(null);

        // AtLeastOnce (pas Once), même raison que ChargerAsync_echec_du_repository_affiche_l_erreur :
        // seul le résultat de CET appel explicite (avec la date invalide) nous intéresse.
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("Date de début"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ChargerPlus_avance_la_page_et_accumule_les_entrees()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        var pageDemandee = new List<int>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<FiltreAuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<FiltreAuditLog, CancellationToken>((f, _) => pageDemandee.Add(f.Page))
            .ReturnsAsync((FiltreAuditLog f, CancellationToken _) =>
                Result.Success<IReadOnlyList<EntreeAuditLog>>(
                    Enumerable.Range(0, FiltreAuditLog.TaillePageParDefaut)
                        .Select(i => Entree with { Id = f.Page * 1000 + i })
                        .ToList()));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object);
        // L'appel fire-and-forget du constructeur (page 1) se termine de manière
        // synchrone (Moq ReturnsAsync => Task déjà complétée, pas de vrai I/O) :
        // il a déjà consommé "1" avant qu'on atteigne cette ligne. On l'efface pour
        // n'observer que les deux appels explicites ci-dessous.
        pageDemandee.Clear();

        await vm.ChargerCommand.ExecuteAsync(null);
        Assert.True(vm.PeutChargerPlus); // page pleine => encore potentiellement des résultats

        await vm.ChargerPlusCommand.ExecuteAsync(null);

        Assert.Equal([1, 2], pageDemandee);
        Assert.Equal(2 * FiltreAuditLog.TaillePageParDefaut, vm.Entrees.Count); // accumulé, pas remplacé
    }

    [Fact]
    public async Task ChargerAsync_page_incomplete_desactive_charger_plus()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<FiltreAuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<EntreeAuditLog>>([Entree])); // 1 < TaillePageParDefaut
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.PeutChargerPlus);
    }
}
