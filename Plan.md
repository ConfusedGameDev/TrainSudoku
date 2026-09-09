We are thinking of a game similar to sudoku


we have a 6 by 6 grid and we are trying to build a rail road.

out of the grid we have an entrance and an exit 

each column has a number indicating how many rail road pieces we need in each column same is true for the rows.

we have a set entrance and exit per level 

we have some set pieces per level

each block allows 1 piece which should have 2 connections 

NS, EW, NW, NE, SW, SE

if there is already a track in the neighbour we must connect to it.

the player must connect from Start  and To End 



the maps will be defined in text base in the following manner



	# # # # # #
	. . . . . .#
	. . . . . .#
	. K . . . .#E
   S. . . . . .#
	. . . . K .#
	. . . . . .#


where
 # is a number 0-6 to indicate how many tracks we have per column and per row

 K is a track key can be any of NS, EW, NW, NE, SW, SE
S is the start point
E is the end point


the player should be able to 

select a block
the system will display available connections 
the player must select 1 connection 
the system will display the remaining connections
the player must select the second connection.


the player can long press on a set piece to erease it.

the game is win when 

1) we have a connecting track from S to E
2) we have all the pieces indicated by the rows and columns



we will use splines to be able to morph a straight track into the selected coordinates the player selected


once the game is win we will generate a spline and display a train animating from start to end.


the game will be a portrait mobile game also compatible with mouse.

we should have a pause menu with the options Resume, Retry or Exit.

we should have a clock displaying how long it is taking you to solve the puzzle.

if the player completes the level for the first time  save the time as the high score and reset the puzzle
if the player completes the level again compare the time and only save the high score 



we need a level editor for the game. 

for now only inside unity editor  using a window editor 

we should be able to save the definition of a game as an asset file and load it from the Game Manager.



M1 
Create the  Level Editor

M2 
Create the user interface

M3 
Create the player field

m4
Create the Spline system for adding pieces 

m5 Create the system to merge 2 splines when we connect 2 pieces


M6 Create the system to Delete one piece and disconnect the Splines 

M7 Create the system to validate a board after each piece is set 

M8 create the system to animate the train to go trough the complete Track

M9 Create the HighScore System

M10 Create the Level Loader System




The Graphics assets are
for the railroad Assets/02.Graphics/Train Assets/railroad-straight.fbx

for the train Assets/02.Graphics/Train Assets/train-electric-bullet-a.fbx Assets/02.Graphics/Train Assets/train-electric-bullet-b.fbx Assets/02.Graphics/Train Assets/train-electric-bullet-c.fbx
for the ui sound Assets/05.Audio/UI
