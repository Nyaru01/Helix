# Tutoriel complet — première recherche avec LocalBlast

Ce dépôt contient maintenant un petit jeu de démonstration téléchargé depuis NCBI. Il permet de vérifier visuellement l'application sans installer d'outil scientifique supplémentaire ni manipuler de gros fichiers.

## Données disponibles

```text
DemoData/
├─ Query/
│  └─ Query_SARS-CoV-2_120nt.fasta       ← séquence à rechercher
└─ SARS-CoV-2/
   ├─ NC_045512.2_Wuhan-Hu-1.fna          ← génome de référence
   ├─ MW276482.1_USA_CA_2020.fna          ← génome complet
   └─ OD980552.1_SARS-CoV-2.fna           ← génome complet
```

La requête est une séquence nucléotidique de 120 bases tirée du début du génome de référence SARS-CoV-2 Wuhan-Hu-1 (`NC_045512.2`). Les trois fichiers `.fna` sont les sujets de recherche. Le fichier de requête est volontairement dans un autre dossier : LocalBlast analyse tous les FASTA présents dans le dossier sélectionné comme des échantillons.

Sources : [NCBI Nucleotide — NC_045512.2](https://www.ncbi.nlm.nih.gov/nuccore/NC_045512.2), [NCBI Nucleotide — MW276482.1](https://www.ncbi.nlm.nih.gov/nuccore/MW276482.1) et [NCBI Nucleotide — OD980552.1](https://www.ncbi.nlm.nih.gov/nuccore/OD980552.1).

## Première recherche, pas à pas

1. Ouvrir **LocalBlast**. L'application est déjà lancée si vous venez de suivre la préparation du projet.
2. Dans la section **1. Query sequence**, cliquer sur **Load query FASTA**.
3. Sélectionner :

   ```text
   D:\Dev\Nico\LocalBlast_WPF_v0.3.0\DemoData\Query\Query_SARS-CoV-2_120nt.fasta
   ```

   Le texte FASTA apparaît dans la grande zone de saisie.
4. Dans la section **2. FASTA collection**, cliquer sur **Browse…** puis choisir le dossier suivant, et non le dossier `Query` :

   ```text
   D:\Dev\Nico\LocalBlast_WPF_v0.3.0\DemoData\SARS-CoV-2
   ```

   Le message doit indiquer **3 compatible FASTA file(s) detected**.
5. Dans **3. BLAST settings**, conserver les valeurs proposées :

   | Champ | Valeur |
   | --- | --- |
   | Program | `blastn` |
   | Minimum identity | `80` % |
   | Minimum query coverage | `80` % |
   | E-value | `1e-10` |

6. Facultatif mais recommandé : cliquer sur **Check bundled BLAST**. Au premier usage, l'application installe automatiquement ses exécutables BLAST locaux. Attendre le message « Bundled BLAST ready ».
7. Cliquer sur **RUN BLAST**. La barre de progression affiche les trois fichiers au fur et à mesure.
8. Une fenêtre **BLAST results** s'ouvre. Le génome de référence devrait être classé `Present`. Les deux autres génomes peuvent être `Below thresholds` : dans ce jeu de démonstration, ils ont une identité parfaite sur un fragment, mais une couverture de requête d'environ 55–59 %, inférieure au seuil initial de 80 %. Les colonnes affichent identité, couverture, contig, coordonnées, E-value et bit score.
9. Double-cliquer une ligne pour ouvrir la fenêtre d'alignement. Les lignes `Query` et `Sbjct` montrent la région comparable ; `|` signifie une base identique et `.` une substitution.

## Interpréter les statuts

| Statut | Signification |
| --- | --- |
| `Present` | Au moins un alignement satisfait les seuils d'identité et de couverture. |
| `Below thresholds` | Un alignement existe, mais son meilleur HSP ne respecte pas un seuil. |
| `No hit` | BLAST ne renvoie aucun alignement pour les paramètres demandés. |
| `Error` | Le fichier ou l'exécution BLAST a produit une erreur ; le détail est visible dans l'alignement. |

Dans cette version, la couverture affichée est la couverture du meilleur HSP (alignement local), pas une couverture cumulée de plusieurs alignements.

## Essayer des variantes

- Mettre une identité minimale à `100` % : les résultats divergents peuvent passer à `Below thresholds`.
- Mettre la couverture à `100` % : seul un alignement couvrant les 120 bases entières pourra être déclaré présent.
- Remplacer une ou plusieurs bases de la requête dans la grande zone de texte, puis relancer la recherche.
- Ajouter vos propres génomes nucléotidiques en `.fna`, `.fa` ou `.fasta` dans un nouveau dossier et conserver `blastn`.

## Choisir le mode adéquat pour ses propres données

| Votre requête | Vos fichiers de référence | Programme |
| --- | --- | --- |
| ADN/ARN | ADN/ARN | `blastn` |
| Protéine | ADN/ARN | `tblastn` |
| Protéine | Protéines | `blastp` |

Les extensions `.fna` et `.ffn` sont considérées comme nucléotidiques ; `.faa` est considéré comme protéique. Pour les autres extensions FASTA, l'application déduit le type à partir de la séquence. Il est préférable de ne pas mélanger protéines et nucléotides dans un même dossier.

## Préparer un vrai jeu de données

1. Créer un dossier distinct pour la requête et un dossier pour les sujets.
2. Mettre un génome/protéome par fichier sujet. Le nom de fichier devient le nom de l'échantillon dans le tableau de résultats.
3. Vérifier que les fichiers sont au format FASTA : une première ligne commençant par `>` suivie d'une ou plusieurs lignes de séquence.
4. Éviter les jeux de données énormes lors des premiers essais : la version actuelle lance BLAST séparément pour chaque fichier.
5. Noter programme, seuils et version des données lorsque l'analyse est destinée à être reproduite ou partagée.
