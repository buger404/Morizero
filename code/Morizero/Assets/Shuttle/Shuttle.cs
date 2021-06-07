using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Shuttle : MonoBehaviour
{
    public Text Score,Combo;
    private int score,combo;
    public Animator Combos;
    public struct HitPoint{
        public float time;
        public int y;
    }
    public TextAsset OsuMap;
    public List<GameObject> Fireworks;
    public GameObject Hithint,UICanvas;
    public RectTransform HitDot;
    private AudioSource source;
    private List<HitPoint> HitPoints = new List<HitPoint>();
    private int nowhit = 0;
    private int nowplay = 0;

    public Sprite[] Animation;                  // 行走图图像集
    public SpriteRenderer image;                // 图形显示容器
    public GameObject Character;
    private float[] yPos = new float[3];
    private int walkBuff = 1;                   // 行走图系列帧序数
    private float walkspan;                     // 行走图动画间隔控制缓冲

    private void Awake() {
        string[] data = OsuMap.text.Split(new char[]{'\r','\n'},System.StringSplitOptions.RemoveEmptyEntries);
        bool start = false;
        int ly = 0;
        foreach(string line in data){
            if(start){
                string[] t = line.Split(',');
                int dy = 0;
                dy = Random.Range(0,3);
                while(dy == ly) dy = Random.Range(0,3);
                ly = dy;
                HitPoints.Add(new HitPoint{
                    time = float.Parse(t[2]) / 1000,
                    y = dy
                });
            }
            if(line == "[HitObjects]") start = true;
        }
        Debug.Log("Osu! resolve:" + HitPoints.Count + " hit objects");
        yPos[1] = Character.transform.localPosition.y;
        yPos[0] = yPos[1] + 0.7f;
        yPos[2] = yPos[1] - 0.7f;
        source = this.GetComponent<AudioSource>();
    }

    // 更新行走图图形
    void UploadWalk(){
        // 行走时的图像
        walkspan += Time.deltaTime;
        if(walkspan > 0.05f){
            walkBuff ++;
            if(walkBuff > 2) walkBuff = 0;
            walkspan = 0;
        }
        // 设定帧
        image.sprite = Animation[walkBuff];
    }
    // ⚠警告：x和y的取值只能为-1，0，1
    float MoveLength(float time = 0){
        float buff = 0.07f * (time == 0 ? Time.deltaTime : time) * 60f;
        return buff;
    }

    void Update()
    {
        Vector3 pos = Character.transform.localPosition;
        pos.x -= MoveLength();
        if(nowplay < HitPoints.Count){
            if(HitPoints[nowplay].time - 1.2f <= source.time){
                HitPoint p = HitPoints[nowplay];
                for(int i = 0;i < 3;i++){
                    if(i != p.y){
                        GameObject go = Instantiate(Fireworks[Random.Range(0,Fireworks.Count)],
                                                    new Vector3(pos.x - MoveLength(1.5f),yPos[i],pos.z),
                                                    Quaternion.identity);
                        go.GetComponent<SpriteRenderer>().sortingOrder = 3 + i;
                    }
                }
                Vector3 dot = HitDot.transform.position;
                GameObject hint = Instantiate(Hithint,new Vector3(dot.x + 10,dot.y,dot.z),Quaternion.identity,UICanvas.transform);
                HintMotion hintMotion = hint.GetComponent<HintMotion>();
                hintMotion.bgm = source;
                hintMotion.rect = hint.GetComponent<RectTransform>();
                hintMotion.ox = HitDot.localPosition.x + 2000;
                hintMotion.tx = HitDot.localPosition.x;
                hintMotion.targetTime = p.time;
                hint.SetActive(true);
                nowplay++;
            }
        }
        if(nowhit < HitPoints.Count){
            if(HitPoints[nowhit].time <= source.time){
                pos.y = yPos[HitPoints[nowhit].y];
                score += Random.Range(800,1200);
                combo++;
                Score.text = score.ToString();
                Combo.text = $"{combo} Combo";
                Combos.Play("TextSrink",0,0f);
                nowhit++;
            }
        }
        Character.transform.localPosition = pos;
        UploadWalk();
    }
}
