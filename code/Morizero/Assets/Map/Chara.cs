using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

using MyNamespace.databridge;
using MyNamespace.rayMapPathFinding.myQueue;

// 用于Drama Script的回调函数
public delegate void WalkTaskCallback();
// 角色控制器
public class Chara : MonoBehaviour
{
    private class _OutMousePositionBuilder : DefaultBridgeTaskBuilder
    {
        public _OutMousePositionBuilder(Component component)
        {
            _product = new BridgeTask();
            _destnaionComponent = component;
        }
        public BridgeTask BuildProduct(Component originComponent,Vector2 parament)
        {
            DefineBridgeParamentType(BridgeParamentType.Chara_MousePosition_RayMapPathFinding);
            BuildOrigin(originComponent);
            BuildParament(parament);
            BuildDestnation(_destnaionComponent);
            return _product;
        }
        
        private Component _destnaionComponent;
    }
    
    private class _RegisterCharacters : DefaultBridgeTaskBuilder
    {
        
    }

    public TDataBridge dataBridge;

    public GameObject adjustableTrigger;
    //public Vector2 outmPos;

    // 行走参数
    public float speed = 0.06f;
    public const float step = 0.48f;
    private float xRemain = 0,yRemain = 0;
    private bool firstFreeMove = false;
    private float freeTouchTick = 0;

    // 朝向枚举
    public enum walkDir{
        Down,Left,Right,Up
    }
    // 行走任务
    public class walkTask{
        public float x,y;                        // 实际的任务坐标
        public float xBuff,yBuff;                // 步数坐标（Drama Script）
        private bool useStep = false;
        public bool isCalculated{
            get{
                return (xBuff == 0 && yBuff == 0);
            }
        }
        public void Caculate(Vector3 pos){
            x = pos.x + (useStep ? Chara.step : 1.0f) * xBuff;
            y = pos.y + (useStep ? Chara.step : 1.0f) * yBuff;
            xBuff = 0; yBuff = 0;
            Debug.Log("Walktask: relative position cale: " + x + "," + y);
        }
        public static walkTask fromRaw(float x,float y){
            return new walkTask{
                x = x,y = y,xBuff = 0,yBuff = 0,useStep = false
            };
        } 
        public static walkTask fromStep(float x,float y){
            return new walkTask{
                x = 0,y = 0,xBuff = x,yBuff = y,useStep = true
            };
        } 
        public static walkTask fromRelative(float x,float y){
            return new walkTask{
                x = 0,y = 0,xBuff = x,yBuff = y,useStep = false
            };
        } 
    }
    // 当列表长度为0时表示行走完毕
    public MyQueueWithIndex<walkTask> walkTasks = new MyQueueWithIndex<walkTask>();

    private Sprite[] Animation;                 // 行走图图像集
    public string Character;                    // 对应的人物
    public bool Controller = false;             // 是否为玩家
    private SpriteRenderer image;               // 图形显示容器
    public walkDir dir;                         // 当前朝向
    private bool walking;                       // 是否正在行走
    private int walkBuff = 1;                   // 行走图系列帧序数
    private float walkspan;                     // 行走图动画间隔控制缓冲
    private float sx,sy,ex,ey;                  // 地图边界x,y - x,y
    public GameObject MoveArrow;                // 点击移动反馈
    private bool tMode = false;                 // 点击移动模式（TouchMode）
    private Vector2 lpos;
    private int lposCount;
    public WalkTaskCallback walkTaskCallback;   // 行走人物回调

    private _OutMousePositionBuilder bridgeTaskbuilderOMP;

    private void Awake() {
        // 载入行走图图像集，并初始化相关设置
        Animation = Resources.LoadAll<Sprite>("Players\\" + Character);
        image = this.GetComponent<SpriteRenderer>();
        // dir = walkDir.Down;
        UploadWalk();
        // 获取地图边界并处理
        Vector3 size = new Vector3(0.25f,0.25f,0f);
        Vector3 pos = GameObject.Find("startDot").transform.localPosition;
        sx = pos.x + size.x; sy = pos.y - size.y; 
        pos = GameObject.Find("endDot").transform.localPosition;
        ex = pos.x - size.x; ey = pos.y + size.y * 1.7f; 
        // 如果是玩家则绑定至MapCamera
        if(Controller) {
            MapCamera.Player = this;
            MapCamera.PlayerCollider = this.transform.Find("Pathfinding").gameObject;
        }
        // 如果是玩家并且传送数据不为空，则按照传送设置初始化
        if(Controller && MapCamera.initTp != -1){
            dir = MapCamera.initDir;
            UploadWalk();
            // 取得传送位置坐标
            this.transform.localPosition = GameObject.Find("tp" + MapCamera.initTp).transform.localPosition;
        }
    }
    // 更新行走图图形
    public void UploadWalk(){
        if(walking){
            // 行走时的图像
            walkspan += Time.deltaTime;
            if(walkspan > 0.1f){
                walkBuff ++;
                if(walkBuff > 2) walkBuff = 0;
                walkspan = 0;
            }
            walking = false;
        }else{
            // 未行走时
            walkspan = 0;
            walkBuff = 1;
        }
        // 设定帧
        image.sprite = Animation[(int)dir * 3 + walkBuff];
    }

    // ⚠警告：x和y的取值只能为-1，0，1
    float Move(int x,int y){
        Vector3 pos = transform.localPosition;
        float buff = speed * Time.deltaTime * 60f * (Input.GetKey(KeyCode.X) ? 2 : 1);
        pos.x += buff * x ;
        pos.y += buff * y ;
        if(pos.x < sx) pos.x = sx;
        if(pos.x > ex) pos.x = ex;
        if(pos.y > sy) pos.y = sy;
        if(pos.y < ey) pos.y = ey;
        transform.localPosition = pos;
        walking = true;
        if(x != 0) dir = x < 0 ? walkDir.Left : walkDir.Right;
        if(y != 0) dir = y < 0 ? walkDir.Down : walkDir.Up;
        return y == 0 ? buff * x : buff * y;
    }

    private void Start()
    {
        if(Controller) // only controller can havve a pathfinding movement
            bridgeTaskbuilderOMP = new _OutMousePositionBuilder(dataBridge.defaultRayMapPathFindingScript);
    }

    private void _SpriteRenderer_AutoSortOrder()
    {

    }

    void FixPos(){
        Vector3 mpos = transform.localPosition;
        mpos.x = Mathf.Round((mpos.x - 0.48f) / 0.96f) * 0.96f + 0.48f; 
        mpos.y = Mathf.Round((mpos.y + 0.48f) / 0.96f) * 0.96f - 0.48f; 
        transform.localPosition = mpos;
    }

    void FixedUpdate()
    {
        // 如果剧本正在进行则退出
        if (MapCamera.SuspensionDrama && walkTasks.Count == 0 && Controller)
        {
            adjustableTrigger.GetComponent<Collider2D>().isTrigger = true;
            return;
        }
        // 是否正在执行行走任务？
        bool isWalkTask = (walkTasks.Count > 0);
        Vector3 pos = transform.localPosition;
        
        // 如果有行走任务
        if(isWalkTask){
            walkTask wt = walkTasks.referencePeek;
            // 如果坐标尚未初始化
            if(!wt.isCalculated) wt.Caculate(pos);
            // 决定是否修正行走坐标（完成行走）
            bool isFix = false;
            if(wt.x < pos.x){
                Move(-1,0);
                if(wt.x >= transform.localPosition.x) isFix = true;
            }else if(wt.x > pos.x){
                Move(1,0);
                if(wt.x <= transform.localPosition.x) isFix = true;
            }else if(wt.y < pos.y){
                Move(0,-1);
                if(wt.y >= transform.localPosition.y) isFix = true;
            }else if(wt.y > pos.y){
                Move(0,1);
                if(wt.y <= transform.localPosition.y) isFix = true;
            }
            if(!Controller) UploadWalk();
            // 修正坐标
            if(isFix){
                Debug.Log("Walktask: " + (walkTasks.Count - 1) + " remaining...");
                FixPos();
                //transform.localPosition = new Vector3(wt.x,wt.y,pos.z);
                walkTasks.Dequeue();
                if(walkTasks.Count == 0){
                    if(tMode){
                        Debug.Log("Walktask: tasks for Pathfinding is done.");
                        tMode = false;
                        MoveArrow.SetActive(false);
                    }else{
                        Debug.Log("Walktask: tasks for Drama Script is done.");
                        walkTaskCallback();
                    } 
                    walking = false;
                    UploadWalk();
                }
            }
        }
        // 如果不是玩家
        if(!Controller) return;

        bool isKeyboard = false;

        // 判定调查
        Vector2 spyRay = new Vector2(pos.x,pos.y);
        if(dir == walkDir.Left){
            spyRay.x -= 0.96f;
        }else if(dir == walkDir.Right){
            spyRay.x += 0.96f;
        }else if(dir == walkDir.Up){
            spyRay.y += 0.96f;
        }else{
            spyRay.y -= 0.96f;
        }
        CheckObj checkObj = null;
        foreach(RaycastHit2D crash in Physics2D.RaycastAll(spyRay,new Vector2(0,0))){
            if(crash.collider.gameObject.TryGetComponent<CheckObj>(out checkObj)){
                if(MapCamera.HitCheck != checkObj.gameObject){
                    checkObj.CheckEncounter();
                }
                break;
            }
        }
        if(checkObj == null && MapCamera.HitCheck != null){
            MapCamera.HitCheck.GetComponent<CheckObj>().CheckGoodbye();
        }

        // 如果屏幕被点击
        if (Input.GetMouseButton(0)){
            if(freeTouchTick < 0.5f){
                freeTouchTick += Time.deltaTime;
            } else if(!isWalkTask && xRemain == 0 && yRemain == 0){
                // 从屏幕坐标换算到世界坐标
                Vector3 mpos = MapCamera.mcamera.GetComponent<Camera>().ScreenToWorldPoint(Input.mousePosition);
                mpos.z = 0;
                // 格式化坐标
                mpos.x = Mathf.Round((mpos.x - 0.48f) / 0.96f) * 0.96f + 0.48f; 
                mpos.y = Mathf.Round((mpos.y + 0.48f) / 0.96f) * 0.96f - 0.48f; 
                if(Mathf.Abs(mpos.x - pos.x) > 0.1f){
                    xRemain = 1.02f * (mpos.x > pos.x ? 1 : -1); firstFreeMove = true; isKeyboard = true;
                }else if(Mathf.Abs(mpos.y - pos.y) > 0.1f){
                    yRemain = 1.02f * (mpos.y > pos.y ? 1 : -1); firstFreeMove = true; isKeyboard = true;
                }
                // 设置点击反馈
                MoveArrow.transform.localPosition = mpos;
                MoveArrow.SetActive(true);
            }
        }
        if (Input.GetMouseButtonUp(0) && !isWalkTask && freeTouchTick < 0.5f && xRemain == 0 && yRemain == 0)
        {
            // 必要：开启tMode，将寻路WalkTask与DramaScript的WalkTask区别开来
            tMode = true;
            walkTasks.Clear();
            // 从屏幕坐标换算到世界坐标
            Vector3 mpos = MapCamera.mcamera.GetComponent<Camera>().ScreenToWorldPoint(Input.mousePosition);
            mpos.z = 0;
            // 检查是否点击的是UI而不是地板
            if (EventSystem.current.IsPointerOverGameObject()) return;
            // 格式化坐标
            mpos.x = Mathf.Floor(mpos.x / 0.96f) * 0.96f + 0.48f - 0.06f;
            mpos.y = Mathf.Ceil(mpos.y / 0.96f) * 0.96f - 0.48f;
            // 设置点击反馈
            MoveArrow.transform.localPosition = mpos;
            MoveArrow.SetActive(true);
            // 设置walkTask保险
            lpos = new Vector2(0,0);
            lposCount = 3;
            //prepare for Event to TRayMapBuilder



            dataBridge.EnqueueTask(bridgeTaskbuilderOMP.BuildProduct(this, mpos));

            goto skipKeyboard;
        }
        if (Input.GetMouseButtonUp(0)) freeTouchTick = 0;

        // 检测键盘输入
        if(!isWalkTask && xRemain == 0 && yRemain == 0){
            if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)){
                xRemain = -1.02f; firstFreeMove = true; isKeyboard = true;
            }else if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)){
                xRemain = 1.02f; firstFreeMove = true; isKeyboard = true;
            }else if(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)){
                yRemain = 1.02f; firstFreeMove = true; isKeyboard = true;
            }else if(Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)){
                yRemain = -1.02f; firstFreeMove = true; isKeyboard = true;
            }
        }
        // 自由移动
        if(xRemain != 0 || yRemain != 0){
            int xDir = xRemain < 0 ? -1 : 1,yDir = yRemain < 0 ? -1 : 1;
            if(xRemain == 0) xDir = 0;
            if(yRemain == 0) yDir = 0;
            if(firstFreeMove){
                Collider2D crash = Physics2D.Raycast(new Vector2(pos.x + xDir,pos.y + yDir),new Vector2(0,0)).collider;
                if(crash != null){
                    if(!crash.isTrigger){
                        if(xDir != 0) dir = xDir < 0 ? walkDir.Left : walkDir.Right;
                        if(yDir != 0) dir = yDir < 0 ? walkDir.Down : walkDir.Up;
                        walking = true;
                        xRemain = 0;yRemain = 0;
                    }
                }
                firstFreeMove = false;
            }
            if(xRemain != 0) {
                xRemain -= Move(xDir,0);
                if((xRemain < 0 ? -1 : 1) != xDir){
                    FixPos();
                    walking = true;
                    if(freeTouchTick == 0) MoveArrow.SetActive(false);
                    xRemain = 0;
                }
            }
            if(yRemain != 0) {
                yRemain -= Move(0,yDir);
                if((yRemain < 0 ? -1 : 1) != yDir){
                    FixPos();
                    walking = true;
                    if(freeTouchTick == 0) MoveArrow.SetActive(false);
                    yRemain = 0;
                }
            }
        }

        skipKeyboard:

        if (Controller && adjustableTrigger!=null)
            adjustableTrigger.GetComponent<Collider2D>().isTrigger = !isKeyboard;

        // 仅打断寻路task（tMode），不打断DramaScript的task
        if (lpos.x == pos.x && lpos.y == pos.y && isWalkTask && tMode) lposCount--;
        if(lposCount == 0 && isWalkTask && tMode){
            Debug.Log("Walktask: tasks for Pathfinding is broke.");
            walking = false;
            UploadWalk();
            walkTasks.Clear(); tMode = false; MoveArrow.SetActive(false);
        }
        lpos = pos;

        // 更新图片
        UploadWalk();

    }
}
