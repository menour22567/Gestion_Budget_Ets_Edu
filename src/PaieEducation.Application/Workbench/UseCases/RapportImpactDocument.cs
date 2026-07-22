namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Enveloppe d'export du <see cref="RapportImpact"/> (chantier P11, audit du
/// 19/07/2026, É4). Évite d'alourdir le record <c>RapportImpact</c> (consommé
/// par les 7 tests unitaires existants et par le moteur de simulation) avec
/// des champs dédiés à l'export PDF : description d'hypothèse, horodatage du
/// dry-run, liste d'erreurs rencontrées.
/// </summary>
/// <remarks>
/// Choix de l'enveloppe (vs extension du record) : la modification du record
/// <see cref="RapportImpact"/> aurait cassé la signature des tests unitaires
/// existants (cf. note É4 de l'audit : « préférer l'enveloppe (ne pas alourdir
/// le record consommé par les tests unitaires existants) »). L'enveloppe porte
/// des champs **strictement documentaires** : le rendu PDF ne s'en sert que
/// pour l'affichage, jamais pour un calcul.
/// </remarks>
/// <param name="Rapport">
/// Rapport d'impact produit par <c>SimulerEvolutionReglementaire</c>. Porté
/// tel quel — non transformé. Ses 6 champs (NbAgents, deltas min/max/total,
/// période, BulletinsAvertis) restent la source de vérité du calcul.
/// </param>
/// <param name="Hypothese">
/// Description textuelle de l'hypothèse d'évolution telle que saisie par
/// l'utilisateur dans l'assistant d'évolution (P8-8a, à venir). Affichée
/// en en-tête du PDF pour archivage et validation hiérarchique. Chaîne vide
/// acceptée (= « pas d'hypothèse saisie »).
/// </param>
/// <param name="Horodatage">
/// Moment du dry-run, capturé par l'<c>IClock</c> au moment où le rapport
/// est généré. Indispensable pour la traçabilité d'archivage : un PDF sans
/// horodatage ne peut pas être rejoué à l'identique si la réglementation
/// évolue à nouveau entre temps.
/// </param>
/// <param name="Erreurs">
/// Liste de messages d'avertissement ou d'erreur rencontrés lors du
/// dry-run (ex. « agent X sans carrière valide, ignoré »). Vide = aucun
/// incident ; liste non-vide = le rapport est livré **avec** un bandeau
/// d'avertissement dans le PDF rendu.
/// </param>
public sealed record RapportImpactDocument(
    RapportImpact Rapport,
    string Hypothese,
    DateTime Horodatage,
    IReadOnlyList<string> Erreurs);
