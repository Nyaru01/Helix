# Analyse de LocalBlast WPF v0.3.0

## Objet du projet

LocalBlast est une application de bureau Windows qui recherche localement une
séquence requête dans une collection de fichiers FASTA, un fichier représentant
un génome ou un échantillon. Elle fournit une interface graphique WPF et appelle
les exécutables officiels NCBI BLAST+ directement sous Windows : aucune donnée
de séquence n'est envoyée à un service distant.

Les trois modes disponibles sont :

| Mode | Requête | Fichiers sujets attendus |
| --- | --- | --- |
| `blastn` | Nucléotidique | Nucléotidiques |
| `tblastn` | Protéique | Nucléotidiques |
| `blastp` | Protéique | Protéiques |

## Fonctionnement

```text
Utilisateur
  └─ Fenêtre principale : requête FASTA + dossier + seuils
       ├─ FastaService : valide/nettoie la requête et sélectionne les FASTA compatibles
       └─ BlastEngine : traite les fichiers, un par un
            └─ NativeBlastService : extrait BLAST+ si nécessaire puis lance blastn/blastp/tblastn
                 └─ Résultats tabulaires BLAST → meilleur HSP → fenêtre de résultats/alignment
```

### Parcours utilisateur

1. L'utilisateur colle une séquence ou charge un fichier FASTA.
2. Il sélectionne le dossier contenant les fichiers FASTA. Seuls les fichiers
   du premier niveau du dossier sont considérés.
3. Il choisit le programme BLAST, l'identité minimale, la couverture minimale
   et l'E-value maximale.
4. Au lancement, la requête est normalisée et écrite dans le dossier temporaire
   Windows (`%TEMP%\LocalBlast\localblast_query.fasta`).
5. Pour chaque FASTA compatible, l'application lance BLAST avec `-subject`.
   La sortie demande notamment l'identifiant du contig, l'identité, `qcovhsp`,
   les coordonnées, les séquences alignées, l'E-value et le bit score.
6. L'application classe le meilleur alignement : `Present` s'il satisfait les
   seuils, `Below thresholds` s'il y a un alignement mais pas aux seuils,
   `No hit` s'il n'y en a aucun, ou `Error` si un fichier ne peut pas être traité.
7. La fenêtre des résultats est alimentée au fil de l'exécution. Un double-clic
   affiche l'alignement Query/Sbjct formaté.

### BLAST embarqué

Le script `prepare-blast.ps1` télécharge BLAST+ NCBI 2.17.0 pour Windows x64,
vérifie son MD5, conserve les trois exécutables nécessaires (`blastn`, `blastp`,
`tblastn`) et les DLL associées dans une archive ZIP. Cette archive est intégrée
à l'exécutable comme ressource.

Lors de la première recherche sur un poste utilisateur, l'application extrait
cette ressource dans :

```text
%LOCALAPPDATA%\LocalBlast\blast\2.17.0\bin
```

Puis elle vérifie l'exécutable avec l'argument `-version`. Le Visual C++
Redistributable x64 peut rester nécessaire sur une installation Windows très
minimaliste.

## Architecture du code

| Élément | Responsabilité |
| --- | --- |
| `App.xaml(.cs)` | Ressources WPF-UI, thème système et ouverture de la fenêtre principale. |
| `MainWindow` | Entrées utilisateur, validation, progression, annulation et orchestration de la recherche. |
| `FastaService` | Validation de la requête, écriture du FASTA temporaire, détection heuristique du type de fichiers. |
| `BlastEngine` | Recherche séquentielle, analyse de la sortie BLAST et sélection du meilleur hit. |
| `NativeBlastService` | Installation locale de BLAST embarqué, exécution de processus et annulation. |
| `ResultsWindow` | Tableau des résultats et résumé par statut. |
| `AlignmentWindow` | Vue détaillée de l'alignement sélectionné. |
| `Models/BlastHit.cs` | Modèles de résultat, progression et exécutable BLAST. |

Le projet cible `net8.0-windows`, utilise WPF et le paquet `WPF-UI` 4.3.0. Il
est publié en exécutable Windows x64 autonome et à fichier unique.

## Lancer le projet

### Prérequis de développement

- Windows 64 bits ;
- .NET SDK 8 ;
- PowerShell et `tar.exe` (inclus dans les versions modernes de Windows) ;
- accès Internet au premier lancement de la préparation, pour télécharger
  l'archive BLAST NCBI (~137 Mo).

### Mode développement

Depuis le dossier racine, lancer :

```powershell
.\run-dev.bat
```

Ce script prépare l'archive BLAST intégrée, puis exécute `dotnet run` sur
`LocalBlast.Wpf.csproj`. La première préparation met l'archive téléchargée en
cache dans `.build-blast` ; les exécutions suivantes la réutilisent.

Équivalent manuel :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare-blast.ps1
dotnet run --project .\LocalBlast.Wpf.csproj
```

### Construire la version distribuable

Lancer :

```powershell
.\build-exe.bat
```

Le script restaure les dépendances, publie une application autonome Win-x64,
puis copie le livrable à la racine sous le nom `LocalBlast.exe`. Ce fichier peut
être transmis à un utilisateur Windows x64 sans SDK .NET, WSL, Python ni
installation BLAST séparée.

### État vérifié dans cet environnement

L'inspection statique est complète, mais une compilation n'a pas été possible
ici : aucun SDK .NET n'est installé. De plus, `Resources` est actuellement vide ;
c'est normal avant l'exécution de `prepare-blast.ps1`, mais le projet ne pourra
pas compiler tant que `Resources\blast-win64-2.17.0.zip` n'aura pas été généré.

## Améliorations recommandées

### Priorité haute

1. **Fiabiliser le cycle de build.** Ajouter une vérification explicite de la
   présence de `tar.exe`, des messages d'erreur actionnables et un contrôle CI
   qui exécute au minimum restauration, compilation et tests. Documenter aussi
   clairement que `prepare-blast.ps1` est obligatoire avant un `dotnet build`.

2. **Ajouter des tests automatisés.** Il n'y a pas de projet de tests. Couvrir
   `NormalizeQuery`, la détection FASTA, le parsing des sorties BLAST, le choix
   du meilleur résultat, les seuils et les coordonnées inversées.

3. **Rendre l'extraction de BLAST atomique.** L'application considère
   l'installation valide si les trois `.exe` existent. Une extraction interrompue
   peut toutefois laisser des DLL absentes ou corrompues. Extraire d'abord dans
   un répertoire temporaire, valider tous les fichiers/une version, puis basculer
   atomiquement vers le répertoire final. Un manifeste avec hashes renforcerait
   encore ce contrôle.

4. **Éviter le fichier temporaire partagé.** Le nom
   `localblast_query.fasta` est fixe. Deux instances de l'application peuvent se
   l'écraser. Créer un sous-dossier temporaire unique par recherche et le nettoyer
   dans un `finally`.

### Priorité fonctionnelle et scientifique

5. **Clarifier la notion de couverture.** La valeur affichée est `qcovhsp`, donc
   la couverture d'un HSP (un alignement local), et non nécessairement la
   couverture cumulée de la requête par plusieurs HSP. Renommer le champ en
   « couverture du meilleur HSP », ou calculer/afficher une couverture agrégée
   si c'est l'intention biologique.

6. **Améliorer le choix du meilleur résultat.** Le classement s'effectue par bit
   score du meilleur HSP, après filtrage. Selon l'usage, il peut être préférable
   de grouper par sujet/contig, de calculer une couverture consolidée et de
   proposer un export de tous les HSP plutôt que de ne retenir qu'un seul.

7. **Passer à une stratégie performante pour les gros jeux de données.** Un
   processus BLAST est lancé séquentiellement pour chaque fichier avec `-subject`.
   C'est simple et approprié aux petits lots, mais coûteux pour des centaines de
   génomes. Prévoir soit un parallélisme borné (avec réglage de threads BLAST),
   soit la construction/gestion de bases `makeblastdb` réutilisables. Mesurer les
   performances avant de choisir ; les deux dimensions de parallélisme ne doivent
   pas être multipliées sans limite.

8. **Rendre l'analyse FASTA plus robuste.** La détection repose sur l'extension
   ou un échantillon de 5 000 caractères et masque les erreurs de lecture en les
   classant implicitement comme incompatibles. Afficher les fichiers illisibles,
   permettre les sous-dossiers si souhaité, et laisser l'utilisateur confirmer un
   type ambigu.

### Priorité ergonomie et maintenabilité

9. **Ajouter l'export et la traçabilité.** Export CSV/TSV/JSON des résultats,
   incluant programme, paramètres, version BLAST, date et chemins source. C'est
   important pour reproduire une analyse.

10. **Séparer l'interface de l'orchestration.** Les fenêtres contiennent la
    logique de validation et de lancement. Une structure MVVM (ViewModels,
    commandes, services injectés) facilitera les tests, la réutilisation et les
    évolutions de l'interface.

11. **Améliorer le retour d'exécution.** Afficher le temps écoulé/restant, les
    détails des erreurs par fichier, la possibilité de relancer les erreurs et
    un bouton pour ouvrir/copier le chemin du FASTA ou du contig.

12. **Gérer les dépendances à long terme.** Mettre en place la mise à jour
    contrôlée de BLAST+ et de WPF-UI, avec un changelog et une vérification de
    compatibilité. MD5 est utile pour correspondre au fichier publié par NCBI,
    mais un hash cryptographique (SHA-256) serait préférable si la source le
    fournit.

## Ordre de mise en œuvre suggéré

1. Tests unitaires et compilation CI.
2. Répertoire temporaire unique + extraction BLAST atomique.
3. Export reproductible et erreurs par fichier.
4. Décision scientifique explicite sur la couverture/HSP.
5. Benchmark puis optimisation des gros lots.
6. Refactorisation MVVM progressive.
