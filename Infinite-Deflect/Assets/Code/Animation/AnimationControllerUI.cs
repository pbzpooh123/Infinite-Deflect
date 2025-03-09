using UnityEngine;
using UnityEngine.UI;

public class AnimationControllerUI : MonoBehaviour
{
    public Animator animator; // อ้างอิงไปยัง Animator ของตัวละคร
    public Button buttonIdle;
    public Button buttonWalk;
    public Button buttonJump;
    public Button buttonHit;

    void Start()
    {
        // ผูกปุ่มกับฟังก์ชันที่เปลี่ยนแปลง animation
        buttonIdle.onClick.AddListener(() => ChangeAnimation("Idle"));
        buttonWalk.onClick.AddListener(() => ChangeAnimation("Walk"));
        buttonJump.onClick.AddListener(() => ChangeAnimation("Jump"));
        buttonHit.onClick.AddListener(() => ChangeAnimation("Hit"));
    }

    void ChangeAnimation(string animationName)
    {
        animator.Play(animationName); // สั่งให้เล่น animation ตามชื่อที่กำหนด
    }
}
