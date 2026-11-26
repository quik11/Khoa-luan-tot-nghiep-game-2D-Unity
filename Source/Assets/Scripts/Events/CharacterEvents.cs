using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using UnityEngine.Events; 
using UnityEngine; 
public class CharacterEvents 
{
   
    public static UnityAction<GameObject, int> characterDamaged;
    public static UnityAction<GameObject, int> characterHealed;
}