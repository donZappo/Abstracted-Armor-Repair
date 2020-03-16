# Abstracted-Armor-Repair

Armor is not automatically repaired after a mission. Now, you will have the overall armor reduced on your 'Mech for each drop depending
on how much armor your 'Mech lost in the previous battle. The 'Mech will have reduced armor until it is repaired. 

Armor is never removed using this mod, it is just reduced during combat based upon damage and then restored at the end of each mission.

These values must be set in the SimGameConstants.json for this mod to have any effect:

"ArmorInstallTechPoints"
"ArmorInstallCost"

Please note that ArmorInstallTechPoints is an int value so if you do not use a mod that scales Mech Tech points in some way then it can be
extremely expensive to fix armor. 
