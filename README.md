# Martingale_Forex : martingale et moyenne

![Statistiques de gains et pertes selon l'heure du jour](http://dpaninfor.fr/Captures/gain%20et%20pertes%20selon%20l'heure%20(martingale_Forex).jpg)
> Statistiques sur la version du robot de la branche github martingale ([3b74e51](https://github.com/abhacid/Robot_Forex/commit/3b74e51718d21c6ee19b8ca5b91b775628b2b768))
***

## Sommaire
Ce projet permet d'écrire un robot de trading basé sur un exemple [Robot_Forex](http://ctdn.com/algos/cbots/show/225) de base écrit par [imWald](http://ctdn.com/users/profile/imWald) sur le dépôt de code source [CTDN](http://ctdn.com).

C'est suite à une demande d'un trader **Antoine C** qui souhaitait ajouter un stop-Loss à ce robot que j'ai commencé l'étude puis la modification de ce dernier. De fil en aiguille j'y ai ajouté d'autres caractéristiques et améliorations.

## Stratégie de Martingale_Forex
Ce robot est une martingale cela signifie qu'il augmente le volume de ses prises de positions lorsqu'il perd afin de moyenner à la hausse ou à la baisse selon qu'il achète ou qu'il vend. le coefficient de martingale `martingaleCoeff` permet de définir le multiplicateur de volume; s'il est à `1` le volume augmente d'une unité du volume initial `FirstLot` ou de façon générale il augmente de `martingaleCoeff*FirstLot`.

Un stop loss et un take profit est appliqué sur chacune des positions mais en se basant sur le prix moyen, c'est à dire le barycentre des prises de positions : `somme(prix*volume)/somme(volume)`.

Pour décider à partir de quel niveau de perte on prends une nouvelle position, on estime l'écart `prix max-prix min` sur une période de 25 bougies et on divise cet écart par 'Max Orders' ce qui donne une grille dont l'écartement varie en fonction de la volatilité des prix, elle est de plus en plus resserrée lorsque la volatilité diminue.

## Interprétation
Les cours fluctuent à la hausse ou à la baisse, mais corrigent régulièrement, c'est ce phénomène de correction qu'utilise ce robot en effet il augmente son exposition lorsqu'il perd de telle façon que la dernière prise de position sera la plus grande en volume, par conséquent dès que la correction arrive les dernières prises de positions permettent d'engranger des gains supérieur aux pertes des premières prises positions. D'autre part le Take profit étant basé sur la moyenne des positions, il est de ce fait atteint plus tôt et diminue de ce fait le risque de pertes importantes.

## Statistiques
Les dernières statistiques (version 1.3.2.0) peuvent être visualisés sur :
[AlgoChart](http://www.algochart.com/report/ozdzm).

## Utilisation
D'un point de vue pratique, il est recommandé de cloner la solution dans le répertoire `C:\Users\{user}\Documents\cAlgo\Sources\Robots`. Cela permettra une intégration parfaite entre cAlgo, Visual studio et Github.
Après avoir compilé le programme sous visual studio 2013, allez dans cAlgo et créez une instance EURUSD avec les paramètres se 
trouvant dans `Backtests and optimisations`.

Vous devez également cloner la librairie [cAlgoBot](https://github.com/abhacid/cAlgoBot) dans votre dossier `C:\Users\{user}\Documents\`.

## Paramètres
Les paramètres à utiliser se trouvent dans le dossier `Backtests and optimisations`.




