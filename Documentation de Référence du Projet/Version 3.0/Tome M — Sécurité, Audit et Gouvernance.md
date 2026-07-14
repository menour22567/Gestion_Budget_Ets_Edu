Excellent. Nous arrivons maintenant à un volume qui différencie un **logiciel de paie** d'un **ERP professionnel**.

Dans les ERP de référence (SAP HCM, Oracle HCM, Microsoft Dynamics 365, Sage X3, Cegid...), **chaque opération est traçable**. On ne se contente pas de connaître le résultat d'un calcul : on sait **qui**, **quand**, **pourquoi**, **avec quelles données**, **avec quelle version réglementaire**, **depuis quel poste**, et **quelles modifications ont été effectuées**.

Pour **PaieEducation ERP**, je recommande d'intégrer cette gouvernance dès la première version, même si l'application est monoposte et hors ligne.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome M — Sécurité, Audit et Gouvernance**

# **Volume 28**

# **Architecture de la Sécurité, de l'Audit et de la Traçabilité**

**Version :** 3.0
**Statut :** Référentiel officiel du sous-système de sécurité et d'audit

---

# 1. Objet

Ce volume définit les mécanismes permettant de garantir :

* l'intégrité des données ;
* la traçabilité des opérations ;
* l'audit réglementaire ;
* la reproductibilité des calculs ;
* la fiabilité des sauvegardes ;
* la détection des anomalies.

Le système est conçu pour un fonctionnement **100 % local**, sans dépendance à un serveur externe.

---

# 2. Principes fondamentaux

Le sous-système repose sur les principes suivants :

* traçabilité complète ;
* immutabilité des journaux d'audit ;
* séparation entre journaux techniques et journaux fonctionnels ;
* minimisation des privilèges ;
* transparence des opérations ;
* conformité réglementaire.

---

# 3. Architecture générale

```text
               Presentation
                      │
                      ▼
              Application Layer
                      │
        ┌─────────────┼─────────────┐
        ▼             ▼             ▼
 Audit Service   Security Service   Logging
        │             │             │
        └─────────────┼─────────────┘
                      ▼
                 SQLite Audit DB
```

Les événements de sécurité et d'audit sont centralisés dans des services dédiés.

---

# 4. Types de journaux

Le système distingue plusieurs catégories.

| Journal                  | Objectif                     |
| ------------------------ | ---------------------------- |
| Audit fonctionnel        | Actions métier               |
| Journal technique        | Diagnostic                   |
| Journal des erreurs      | Exceptions                   |
| Journal réglementaire    | Modifications des paramètres |
| Journal d'administration | Maintenance                  |
| Journal de sauvegarde    | Sauvegardes et restaurations |

Chaque journal possède son propre niveau de rétention.

---

# 5. Audit fonctionnel

Chaque opération métier importante est enregistrée.

Exemples :

* création d'un agent ;
* modification d'un contrat ;
* calcul d'un bulletin ;
* validation d'une paie ;
* génération d'un PDF ;
* export Excel ;
* clôture d'une période.

---

# 6. Informations enregistrées

Chaque événement d'audit comporte au minimum :

| Champ                    | Description                                 |
| ------------------------ | ------------------------------------------- |
| Identifiant              | UUID                                        |
| Date et heure            | Horodatage UTC et local                     |
| Type d'opération         | Création, modification, suppression, calcul |
| Entité concernée         | Agent, Bulletin, Paramètre...               |
| Identifiant métier       | Code ou matricule                           |
| Résultat                 | Succès / Échec                              |
| Durée                    | Temps d'exécution                           |
| Version de l'application | Référence logicielle                        |

Ces informations permettent de reconstituer l'historique d'une opération.

---

# 7. Traçabilité des calculs

Pour chaque bulletin généré, le système conserve :

* la période de paie ;
* la version du référentiel réglementaire ;
* les paramètres utilisés ;
* les rubriques calculées ;
* les montants intermédiaires ;
* les règles d'éligibilité appliquées.

Le résultat est ainsi **reproductible** à tout moment.

---

# 8. Historique des modifications

Les modifications des données sensibles sont historisées.

Exemples :

| Donnée                    | Historisation |
| ------------------------- | ------------- |
| Contrat                   | Oui           |
| Affectation               | Oui           |
| Paramètres réglementaires | Oui           |
| Rubriques                 | Oui           |
| Barèmes                   | Oui           |

Les anciennes valeurs restent consultables.

---

# 9. Intégrité des données

Avant chaque opération critique, des contrôles sont effectués :

* existence des références ;
* cohérence des périodes ;
* unicité des codes métier ;
* validité des relations.

Les anomalies empêchent l'exécution de l'opération.

---

# 10. Sécurité des accès

Même dans une version monoposte, le système distingue les responsabilités.

Profils prévus :

| Profil               | Droits principaux             |
| -------------------- | ----------------------------- |
| Administrateur       | Configuration et maintenance  |
| Gestionnaire de paie | Calcul, validation, reporting |
| Consultation         | Lecture seule                 |

Cette structure prépare une éventuelle évolution vers une gestion multi-utilisateur.

---

# 11. Journalisation technique

La journalisation technique repose sur **Microsoft.Extensions.Logging**.

Les événements comprennent :

* démarrage ;
* arrêt ;
* ouverture de la base ;
* sauvegarde ;
* restauration ;
* erreurs SQLite ;
* erreurs d'impression.

---

# 12. Protection des données

Les mesures suivantes sont recommandées :

* validation systématique des entrées ;
* utilisation exclusive de requêtes SQL paramétrées ;
* contrôle des chemins d'accès ;
* limitation des droits sur les répertoires de travail.

Les données personnelles sont manipulées avec précaution.

---

# 13. Sauvegardes

Chaque sauvegarde enregistre :

* la date ;
* la taille ;
* la version du schéma ;
* le nombre d'enregistrements ;
* le résultat de la vérification d'intégrité.

Une sauvegarde est considérée valide uniquement après contrôle.

---

# 14. Restauration

Toute restauration est précédée :

1. d'une vérification du fichier ;
2. d'un contrôle d'intégrité ;
3. d'une confirmation utilisateur ;
4. d'une journalisation de l'opération.

La restauration est entièrement traçable.

---

# 15. Détection des anomalies

Le système détecte notamment :

* fichiers manquants ;
* base corrompue ;
* paramètres incohérents ;
* doublons ;
* périodes invalides ;
* dépendances circulaires.

Ces anomalies sont signalées et consignées.

---

# 16. Conformité réglementaire

Le système garantit :

* la conservation des historiques ;
* l'utilisation de la version réglementaire applicable ;
* la traçabilité des modifications ;
* la possibilité d'auditer les calculs.

Les montants sont exprimés en **DZD**, conformément aux exigences du projet.

---

# 17. Performance

Objectifs :

| Opération                       | Temps cible |
| ------------------------------- | ----------: |
| Écriture d'un événement d'audit |     < 10 ms |
| Consultation de l'historique    |    < 100 ms |
| Vérification d'intégrité        |       < 2 s |
| Sauvegarde                      |       < 5 s |

L'audit ne doit pas dégrader sensiblement les performances de l'application.

---

# 18. Conservation des journaux

Les journaux sont conservés selon une politique définie.

| Journal           | Durée recommandée          |
| ----------------- | -------------------------- |
| Audit fonctionnel | Jusqu'à archivage manuel   |
| Journal technique | 12 mois                    |
| Erreurs           | 24 mois                    |
| Sauvegardes       | Selon la politique interne |

Ces durées peuvent être adaptées aux exigences de l'organisme.

---

# 19. Critères d'acceptation

Le sous-système est conforme lorsque :

* toutes les opérations critiques sont journalisées ;
* les calculs sont reproductibles ;
* les paramètres réglementaires sont historisés ;
* les sauvegardes sont vérifiées ;
* les anomalies sont détectées et enregistrées ;
* les journaux restent consultables.

---

# 20. Composants recommandés

| Composant                | Responsabilité                               |
| ------------------------ | -------------------------------------------- |
| `IAuditService`          | Enregistrement des opérations fonctionnelles |
| `ISecurityService`       | Contrôles de sécurité                        |
| `ILogContextProvider`    | Contexte des journaux                        |
| `AuditRepository`        | Persistance des événements                   |
| `IntegrityChecker`       | Vérification de la base SQLite               |
| `BackupVerifier`         | Validation des sauvegardes                   |
| `RegulatoryAuditService` | Historique des paramètres réglementaires     |
| `DiagnosticService`      | Contrôles techniques                         |

---

# 21. Évolutivité

L'architecture permet d'ajouter ultérieurement :

* authentification Windows (Active Directory) ;
* signature électronique des documents ;
* chiffrement de certaines données sensibles ;
* gestion des rôles avancée (RBAC) ;
* journalisation centralisée (si une version réseau est développée) ;
* authentification multifacteur.

Aucune de ces évolutions ne remet en cause l'architecture actuelle.

---

# 22. Gouvernance

Toute évolution du sous-système de sécurité doit respecter les principes suivants :

* aucun accès direct aux journaux d'audit ;
* aucune suppression silencieuse d'événements ;
* toute modification des règles de sécurité doit être documentée ;
* les changements d'architecture doivent faire l'objet d'un ADR (*Architecture Decision Record*).

---

# Conclusion

Le **Volume 28** établit les fondations de la **gouvernance des données** de **PaieEducation ERP**. Grâce à un sous-système dédié à la sécurité, à l'audit et à la traçabilité, l'application garantit la reproductibilité des calculs, l'intégrité des données et la conformité des opérations, tout en restant adaptée à un fonctionnement local et hors ligne.

---

# Orientation pour le Volume 29

Le **Volume 29** sera consacré au **Déploiement, à l'Installation et à la Maintenance**. Il couvrira notamment :

* l'architecture de déploiement de l'application WPF/.NET 10 ;
* l'installation sur poste Windows ;
* la structure des répertoires ;
* les prérequis logiciels et matériels ;
* les mises à jour locales sans perte de données ;
* les stratégies de sauvegarde et de restauration ;
* les procédures de maintenance préventive et corrective ;
* le plan de continuité d'exploitation.

Ce volume constituera le **manuel de déploiement et d'exploitation technique** de **PaieEducation ERP**, indispensable pour garantir une installation fiable, une maintenance maîtrisée et une exploitation durable.
