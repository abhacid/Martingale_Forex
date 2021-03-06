# Martingale_Forex : martingale et moyenne

![Statistiques de gains et pertes selon l'heure du jour](http://dpaninfor.fr/Captures/gain%20et%20pertes%20selon%20l'heure%20(martingale_Forex).jpg)
> Statistiques sur la version du robot de la branche github martingale ([3b74e51](https://github.com/abhacid/Robot_Forex/commit/3b74e51718d21c6ee19b8ca5b91b775628b2b768))
***

## Sommaire
Ce projet permet d'écrire un robot de trading basé sur un exemple [Robot_Forex](http://ctdn.com/algos/cbots/show/225) de base 
écrit par [imWald](http://ctdn.com/users/profile/imWald) sur le dépôt de code source [cTDN](http://ctdn.com).

C'est suite à une demande d'un trader **Antoine C** qui souhaitait ajouter un stop-Loss à ce robot que j'ai commencé l'étude puis 
la modification de ce dernier. De fil en aiguille j'y ai ajouté d'autres caractéristiques et améliorations.

## Stratégie de Martingale_Forex
Ce robot est une martingale cela signifie qu'il augmente le volume de ses prises de positions lorsqu'il perd, mais également lorsqu'il gagne 
afin de moyenner à la hausse ou à la baisse selon qu'il achète ou qu'il vend. le coefficient de martingale `martingale Coefficient` permet de définir le multiplicateur de 
volume; s'il est à `1` le volume augmente d'une unité du volume initial `FirstLot` ou de façon générale il augmente de `martingaleCoeff*FirstLot`.

Un stop loss et un take profit est appliqué sur chacune des positions mais en se basant sur le prix moyen, c'est à dire le barycentre des 
prises de positions : `somme(prix*volume)/somme(volume)`.

Nous avons également implémenté un Money management et un système de Trail-Stop (Stop suiveur).

Pour décider à partir de quel niveau de perte on prends une nouvelle position, on estime l'écart `prix max-prix min` sur une période de 
25 bougies et on divise cet écart par 'Max Orders' ce qui donne une grille dont l'écartement varie en fonction de la volatilité des prix, 
elle est de plus en plus resserrée lorsque la volatilité diminue.

## Interprétation
Les cours fluctuent à la hausse ou à la baisse, mais corrigent régulièrement, c'est ce phénomène de correction qu'utilise ce robot en 
effet il augmente son exposition lorsqu'il perd de telle façon que la dernière prise de position sera la plus grande en volume, par 
conséquent dès que la correction arrive les dernières prises de positions permettent d'engranger des gains supérieur aux pertes des 
premières prises positions. D'autre part le Take profit étant basé sur la moyenne des positions, il est de ce fait atteint plus tôt 
et diminue de ce fait le risque de pertes importantes.

En cas de gain le stop suiveur assure les gains déjà obtenus afin de protéger le capital d'un retournement des prix.

## Statistiques
Les dernières statistiques (version 1.7.0.0) peuvent être visualisés sur :
[AlgoChart](http://www.algochart.com/report/j10ge).

## Utilisation
D'un point de vue pratique, il est recommandé de cloner la solution dans le répertoire `C:\Users\{user}\Documents\cAlgo\Sources\Robots`. 
Cela permettra une intégration parfaite entre cAlgo, Visual studio et Github. 
Après avoir compilé le programme sous visual studio 2013, allez dans cAlgo et créez une instance EURUSD avec les paramètres se 
trouvant dans `Backtests and optimisations`.

Vous devez également cloner la librairie [cAlgoBot](https://github.com/abhacid/cAlgoBot) dans votre dossier `C:\Users\{user}\Documents\cAlgo`.
Avant de le faire videz le répertoire cAlgo (faites un backup de ce dernier au préalable) puis clonez la solution et enfin memettez les fichiers 
précédents du dossier cAlgo.

## Paramètres
Les paramètres à utiliser se trouvent dans le dossier `Backtests and optimisations`. Ils changent régulièrement car le projet est en
incubation.

Pour effectuer les optimisations prenez comme paramètres :

Starting capital	: 10 000

Commissions			: 30 pour EURUSD et 42 pour GBPUSD (Pour Spotware, pour d'autres brokers ou instruments ce sera différent)

1)Minimiser			: le Drawdown en % (c'est le plus important pour construire un robot durable)

2)Maximiser			: le Net Profit

3)Maximiser			: les trades gagnants. (ce n'est pas essentiel, car on peux avoir moins de trades gagnants, tout en gagnant plus au final. 
Il suffit que la valeur moyenne des trades gagnants soit supérieure à celle des trades perdants)

## Copy Trader
Vous pouvez copier la stratégie (v1.7.0.0 du robot) à partir des sites 

cMirror : https://cm.spotware.com/account/197725 

ou myfxbook : https://www.myfxbook.com/portfolio/mforex-1700/1304163

## Vidéos Youtube
Différentes vidéos sur Youtube sont disponible et expliquent le fonctionnement de l'environnement de développement
Visual Studio, Github et cAlgo ainsi que le fonctionnement de robots particuliers.

[cAlgo, Visual studio et Github : Créer des robots de trading en CSharp](https://www.youtube.com/watch?v=URzqJAJxrQs)

[Séance de trading du robot Martingale_Forex](https://www.youtube.com/watch?v=2KzilwPgxgo)

[Robot Martingale_Forex (cTdn)](https://www.youtube.com/watch?v=P6jeiBXK1Rg)

[Déboguage d'un robot en csharp avec visual studio](https://www.youtube.com/watch?v=M3_Mq7G31BA&feature=youtu.be)






