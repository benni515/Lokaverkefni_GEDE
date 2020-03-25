using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateBlock : MonoBehaviour
{
    public bool isGrounded;
    private World world;

    public float gravity = -0.2f;

    public float playerWidth = 0.8f;
    public float playerHeight = 0.8f;


    private float horizontal;
    private float vertical;
    private float verticalMomentum = 0.5f;
    private Vector3 velocity;




    private void Start() {
        world = GameObject.Find("World").GetComponent<World>();
    }

    private void FixedUpdate() {
        CalculateVelocity();


        transform.Rotate(new Vector3(0,1,0) * 50 * Time.deltaTime);
        transform.Translate(velocity, Space.World);
    }



    private void CalculateVelocity() {
        // Affect vertical momentum with grav

        // Apply falling
        velocity += Vector3.up * -0.5f * Time.fixedDeltaTime;

        if ((velocity.z > 0 && front) || (velocity.z < 0 && back))
            velocity.z = 0;
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
            velocity.x = 0;

        if (velocity.y < 0)
            velocity.y = CheckDownSpeed(velocity.y);
        else if (velocity.y > 0)
            velocity.y = CheckUpSpeed(velocity.y);
    }

    private float CheckDownSpeed(float downSpeed) {
        // Check if player is standing on a block, need to check around him
        if (
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed - 0.4f, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed - 0.4f, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed - 0.4f, transform.position.z + playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed - 0.4f, transform.position.z + playerWidth))
            ) {
            isGrounded = true;
            return 0;
        }
        else {
            isGrounded = false;
            return downSpeed;
        }
    }

    private float CheckUpSpeed(float upSpeed) {
        // Check if player is standing on a block, need to check around him
        if (
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z + playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2.0f + upSpeed, transform.position.z + playerWidth))
            ) {
            isGrounded = true;
            return 0;
        }
        else {
            isGrounded = false;
            return upSpeed;
        }
    }


    public bool front {
        get {
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

    public bool back {
        get {
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

    public bool left {
        get {
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
        get {
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






}


    










