# Robot_Forex : martingale et moyenne

## Sommaire
Ce projet permet d'écrire un robot de trading basé sur un exemple [Robot_Forex](http://ctdn.com/algos/cbots/show/225) de base écrit par [imWald](http://ctdn.com/users/profile/imWald) sur le dépôt de code source [CTDN](http://ctdn.com).

C'est suite à une demande d'un trader **Antoine C** qui souhaitait ajouter un stop-Loss à ce robot que j'ai commencé l'étude puis la modification de ce dernier. De fil en aiguille j'y ai ajouté d'autres caractéristiques et améliorations.

## Stratégie de Robot_Forex
Ce robot est une martingale cela signifie qu'il augmente le volume de ses prises de positions lorsqu'il perd afin de moyenner à la hausse ou à la baisse selon qu'il achète ou qu'il vend. le coefficient de martingale `martingaleCoeff` permet de définir le multiplicateur de volume; s'il est à `1` le volume augmente d'une unité du volume initial `FirstLot` ou de façon générale il augmente de `martingaleCoeff*FirstLot`.

Un stop loss et un take profit sont appliqués sur chacune des positions mais en se basant sur le prix moyen, c'est à dire le barycentre des prises de positions : `somme(prix*volume)/somme(volume)`.

Pour décider à partir de quel niveau de perte on prends une nouvelle position, on estime l'écart `prix max-prix min` sur une période de 25 bougies et on divise cet écart par 4 ce qui donne une grille dont l'écartement varie en fonction de la volatilité des prix, elle est de plus en plus resserrée lorsque la volatilité diminue.

## Interprétation
Les cours fluctuent à la hausse ou à la baisse, mais corrigent régulièrement, c'est ce phénomène de correction qu'utilise ce robot en effet il augmente son exposition lorsqu'il perd de telle façon que la dernière prise de position sera la plus grande en volume, par conséquent dès que la correction arrive les dernières prises de positions permettent d'engranger des gains supérieur aux pertes des premières prises positions. D'autre part le Take profit étant basé sur la moyenne des positions, il est de ce fait atteint plus tôt et diminue de ce fait le risque de pertes importantes.
