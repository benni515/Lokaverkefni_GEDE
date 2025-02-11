﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public bool isGrounded;
    public bool isSprinting;
    private Transform cam;
    private World world;

    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float jumpForce = 5f;
    public float gravity = -9.8f;

    public float playerWidth = 0.3f;
    public float playerHeight = 1.8f;
    public float MouseSpeed = 5.0f;


    private float horizontal;
    private float vertical;
    private float mouseHorizontal;
    private float mouseVertical;
    private Vector3 velocity;
    private float verticalMomentum;
    private bool jumpRequest;

    // Code for higlight and placeblock

    public Transform highlightBlock;
    public Transform placeBlock;
    public float checkIncrement = 0.1f;
    public float reach = 8f;

    public Text selectedBlockText;
    public byte selectedBlockIndex = 1;


    private void Start()
    {
        cam = GameObject.Find("Main Camera").transform;
        world = GameObject.Find("World").GetComponent<World>();

        Cursor.lockState = CursorLockMode.Locked;
        selectedBlockText.text = world.blocktypes[selectedBlockIndex].blockName + " : block seleceted";
        placeBlock.gameObject.SetActive(false);
        highlightBlock.gameObject.SetActive(false);


    }

    private void FixedUpdate()
    {
        CalculateVelocity();

        if (jumpRequest)
            Jump();

        transform.Rotate(Vector3.up * mouseHorizontal * MouseSpeed);
        cam.Rotate(Vector3.right * -mouseVertical * MouseSpeed);
        transform.Translate(velocity, Space.World);
    }

    private void Update()
    {
        GetPlayerInputs();
        placeCursorBlock();
    }

    void Jump()
    {
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    private void CalculateVelocity()
    {
        // Affect vertical momentum with grav
        if (verticalMomentum > gravity)
            verticalMomentum += Time.fixedDeltaTime * gravity;

        // Sprinting
        if (isSprinting)
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * sprintSpeed;
        else
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * walkSpeed;

        // Apply falling
        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

        if ((velocity.z > 0 && front) || (velocity.z < 0 && back))
            velocity.z = 0;
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
            velocity.x = 0;

        if (velocity.y < 0)
            velocity.y = CheckDownSpeed(velocity.y);
        else if (velocity.y > 0)
            velocity.y = CheckUpSpeed(velocity.y);
    }

    private void GetPlayerInputs()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        // Sprint
        if (Input.GetButtonDown("Sprint"))
            isSprinting = true;
        if (Input.GetButtonUp("Sprint"))
            isSprinting = false;

        // Jump
        if (isGrounded && Input.GetButtonDown("Jump"))
            jumpRequest = true;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if(scroll != 0) {
            if(scroll > 0) {
                selectedBlockIndex += 1;
            } else {
                selectedBlockIndex -= 1;
            }
            if(selectedBlockIndex > (byte)(world.blocktypes.Length - 1)) {
                selectedBlockIndex = 1;
            }
            if(selectedBlockIndex < 1) {
                selectedBlockIndex = (byte)(world.blocktypes.Length - 1);
            }
            selectedBlockText.text = world.blocktypes[selectedBlockIndex].blockName + " : block seleceted";
        }

        if(highlightBlock.gameObject.activeSelf) {
            // Destroy block
            if(Input.GetMouseButtonDown(0)) {
                world.GetChunkFromVector3(highlightBlock.position).EditVoxel(highlightBlock.position, 0);
            }

            // Place block

        }
        if (placeBlock.gameObject.activeSelf) {
            if (Input.GetMouseButtonDown(1)) {
                world.GetChunkFromVector3(placeBlock.position).EditVoxel(placeBlock.position, selectedBlockIndex);
            }
        }
    }

    private float CheckDownSpeed(float downSpeed)
    {
        // Check if player is standing on a block, need to check around him
        if (
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth))
            )
        {
            isGrounded = true;
            return 0;
        } 
        else
        {
            isGrounded = false;
            return downSpeed;
        }
    }

    private float CheckUpSpeed(float upSpeed)
    {
        // Check if player is standing on a block, need to check around him
        if (
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z + playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z + playerWidth))
            )
        {
            isGrounded = true;
            return 0;
        }
        else
        {
            isGrounded = false;
            return upSpeed;
        }
    }


    public bool front
    {
        get
        {
            // Check both block player is occupying
            if (
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z + playerWidth)) ||
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth))
                )
                return true;
            else
                return false;
        }
    }

    public bool back
    {
        get
        {
            // Check both block player is occupying
            if (
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z - playerWidth)) ||
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth))
                )
                return true;
            else
                return false;
        }
    }

    public bool left
    {
        get
        {
            // Check both block player is occupying
            if (
                world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y, transform.position.z)) ||
                world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z))
                )
                return true;
            else
                return false;
        }
    }

    public bool right
    // Code for hightlight and place block
    {
        get
        {
            // Check both block player is occupying
            if (
                world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y, transform.position.z)) ||
                world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z))
                )
                return true;
            else
                return false;
        }
    }

    // Code for hightlight and place block
    private void placeCursorBlock() {
        float step = checkIncrement;
        Vector3 lastPos = new Vector3();


        while(step < reach) {
            Vector3 pos = cam.position + (cam.forward * step);

            if(world.CheckForVoxel(pos)) {
                highlightBlock.position = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
                placeBlock.position = lastPos;

                highlightBlock.gameObject.SetActive(true);
                placeBlock.gameObject.SetActive(true);

                return;
            }
            lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step += checkIncrement;
        }

        placeBlock.gameObject.SetActive(false);
        highlightBlock.gameObject.SetActive(false);


    }















}
