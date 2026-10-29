using UnityEngine;

public class GrappleTarget : MonoBehaviour
{
    [SerializeField] private bool available = true;
    public bool IsAvailable => available && isActiveAndEnabled;
    public void SetAvailable(bool value)
    {
        available = value;
    }
}
