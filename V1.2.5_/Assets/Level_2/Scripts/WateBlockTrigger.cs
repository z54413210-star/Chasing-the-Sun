using UnityEngine;
 public class WaterBlockTrigger : MonoBehaviour { 
    private void OnTriggerEnter2D(Collider2D other)
     { if (other.CompareTag("Box")) { Debug.Log("过去：堵住泉眼");
      GameState.Instance.isWaterBlocked = true; }
       } 
       }