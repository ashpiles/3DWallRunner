using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Helper;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;

namespace game
{
   
    [CreateAssetMenu(fileName = "PlayerController", menuName = "Controller/PlayerController")]
    public class PlayerController : Controller
    {
        public float movementLerpSpeed = 10f;
        public float ControlRotationSensitivity = 1.0f;

        public InputHandler playerInput;
        public PlayerCamera playerCam;
        public CoroutineLauncher launcher;
        private float movementInputWeight = 1f;
        private float crouchWeight = 1f;
        private float acceleration => character.hasMovementInput ? character.movement.acceleration : character.movement.decceleration; 

        private SubstateMachine mainStates = new SubstateMachine();
        public override void Init(Character character)
        {
            this.character = character;

            playerInput = InputHandler.Instance;
            playerCam = PlayerCamera.Instance;
            launcher = this.character.GetComponent<CoroutineLauncher>();
            SetSurfaceCollisions();
            this.character.movement.substates = new SubstateMachine();
            this.character.movement.substates.AddState(Void);
            this.character.movement.substates.AddState(Running);
            this.character.movement.substates.AddState(Crouching);

            // set substates 
            this.character.surfaceCollisions.substates = new SubstateMachine();
            this.character.surfaceCollisions.substates.AddState(Void);
            this.character.surfaceCollisions.substates.AddState(JustWalkedOffLedge);
            this.character.surfaceCollisions.substates.AddState(OnWall);
            this.character.surfaceCollisions.substates.AddState(OnSlope);
            this.character.surfaceCollisions.substates.AddState(LeftSurface);

            mainStates.AddState(OnGround);
            mainStates.AddState(InAir);
            mainStates.AddState(OnSurface);

        }


        public override void OnCharacterUpdate()
        {
            playerInput.TickInput(Time.deltaTime); 
            UpdateControlRotation();
            character.SetMovementInput(GetMovementInput());
            character.SetJumpInput(playerInput.jumpInput > 0f);
            UpdateSurfaceStates();
            UpdateMovementStates();
        }

        public override void OnCharacterFixedUpdate()
        {
            playerCam.SetPosition(character.transform.position);
            playerCam.SetControlRotation(character.GetControlRotation());
        }
 

        private void UpdateControlRotation()
        {
            Vector2 camInput = playerInput.mouseInput;
            Vector2 controlRotation = character.GetControlRotation();

            // Adjust the pitch angle (X Rotation)
            float pitchAngle = controlRotation.x;
            pitchAngle -= camInput.y * ControlRotationSensitivity;

            // Adjust the yaw angle (Y Rotation)
            float yawAngle = controlRotation.y;
            yawAngle += camInput.x * ControlRotationSensitivity;

            controlRotation = new Vector2(pitchAngle, yawAngle);
            character.SetControlRotation(controlRotation);
        }

        private Vector3 GetMovementInput()
        {

            Quaternion yawRotation = Quaternion.Euler(0.0f, character.GetControlRotation().y, 0.0f);
            Vector3 forward = yawRotation * Vector3.forward;
            Vector3 right = yawRotation * Vector3.right; 
            // Interpolate the movement input over time
            float weight = movementInputWeight * acceleration;
            Vector3 targetMovementInput = (forward * playerInput.movementInput.y + right * playerInput.movementInput.x) + (character.horizontalVelocity * weight);
            targetMovementInput.Normalize(); // Normalize the target input 
            return Vector3.Lerp(character.GetMovementInput(), targetMovementInput, Time.deltaTime * movementLerpSpeed);
            
        }

        /// <summary>
        /// Parameters for Character BoxCast
        /// index: 0 Must be a ground check
        /// </summary>
        private void SetSurfaceCollisions()
        {
            character.surfaceCollisions.numOfBoxes = 4;
            int boxes = character.surfaceCollisions.numOfBoxes;
            float scaleWeight = .3f;

            // 0 = ground check 
            // 1 = left check | 2 = right check | 3 = forward check 
            character.surfaceCollisions.surfaceLayers = 1 << 8;
            //Debug.Log(character.surfaceCollisions.boxCastScale);
            Vector2 scale = character.characterColliderScale; 
            character.surfaceCollisions.boxCastPositions = new Vector3[] 
                { Vector3.down, Vector3.left * .5f, Vector3.right * .5f, Vector3.forward * .5f};
            character.surfaceCollisions.boxCastScale = new Vector3[]
            //changed the first line below this line from scale.x * 2 to scale.x / 2 smae with the z axis so that the ground collision box is small and we dont need it to intersect with other surfaces only the side ones should
                { new Vector3(scale.x / 2, scale.x * .7f, scale.x / 2), 
                    new Vector3(scale.x * scaleWeight , scale.y * .6f,scale.x * 2),
                    new Vector3(scale.x * scaleWeight, scale.y * .6f, scale.x * 2),
                    new Vector3(scale.x * 2, scale.y * .6f, scale.x * scaleWeight)};
            character.surfaceCollisions.boxRotations = new Vector3[] 
            { Vector3.down, Vector3.left, Vector3.right, Vector3.forward }; 
            character.surfaceCollisions.surfaceNormals = new Vector3[boxes];

        }

        #region player state machines

        // really need to get the functions out of this thing
        // could just use a substate
        // verticalSpeed = var mutated by a substate function
        private void UpdateSurfaceStates()
        {
            //Debug.Log(character.surfaceCollisions.currentNormal);
            if (character.surfaceCollisions.currentNormal != Vector3.zero)
            {
                mainStates.Run(OnSurface);
                character.surfaceCollisions.substates.SetCurrent(OnWall); // do some shennigans to find out if a wall or slope
            }
            else if (character.surfaceCollisions.surfaceNormals[0].y > 0)
            {
                mainStates.Run(OnGround);
                character.surfaceCollisions.substates.SetCurrent(Void);
            } 
            else
            {
                if (mainStates!= InAir)
                { character.surfaceCollisions.substates.Run(JustWalkedOffLedge); }
                else
                { character.surfaceCollisions.substates.SetCurrent(Void); }
                mainStates.Run(InAir);

                if (character.surfaceCollisions.substates == JustWalkedOffLedge)
                { character.surfaceCollisions.justWalkedOffEdge = true; }
                else
                    character.surfaceCollisions.justWalkedOffEdge = false; 
            }

        }

        private void UpdateMovementStates()
        {
            ref SubstateMachine  moveStates = ref character.movement.substates; 
            if(playerInput.sprintInput > 0f)
            { 
                moveStates.Run(Running);
                crouchWeight = 1f;
            }
            else if (playerInput.crouchInput > 0f)
            {
                moveStates.Run(Crouching);
            }
            else
            {
                character.movement.maxHorizontalSpeed = 8f;
                crouchWeight = 1;
            }
         }

        
        #endregion

        /// <summary>
        /// Tab over the nested substates
        /// /// </summary> 
        #region substate Actions
 
        // Surface
        private void JustWalkedOffLedge() 
        {
            launcher.Launch(character.surfaceCollisions.substates.HoldCurrentStateTill());
        }
        private void OnWall()
        {
            character.surfaceCollisions.castDistance = 3f;
            if (!character.surfaceCollisions.substates.lockState)
                launcher.Launch(character.surfaceCollisions.substates.SwitchToAfter( () => !(mainStates == OnSurface), LeftSurface));
        }
        private void LeftSurface()
        {
            character.surfaceCollisions.castDistance = 2f;
        }
        private void OnSlope() { }
        // Main States
        private void OnGround()
        {
            movementInputWeight = 0;

            character.SetVerticalSpeed = (deltaTime) =>
            { 
                float verticalSpeed;
                verticalSpeed = -character.gravitySettings.groundedGravity;
                if (character.isJumpInput)
                {
                    verticalSpeed = character.movement.jumpSpeed;
                }
                return verticalSpeed;
            };
            character.SetHorizontalSpeed = (deltaTime) =>
            {
                Vector3 movementInput = character.GetMovementInput();
                float horizontalSpeed = character.getHorizontalSpeed;
                if (movementInput.sqrMagnitude > 1.0f)
                {
                    movementInput.Normalize();
                } 
                character.movement.targetHorizontalSpeed = movementInput.magnitude * character.movement.maxHorizontalSpeed;
                return Mathf.MoveTowards(horizontalSpeed, character.movement.targetHorizontalSpeed, acceleration * deltaTime); 

            };

        }
        private void InAir()
        {
            movementInputWeight = .001f;
            character.SetVerticalSpeed = (deltaTime) =>
            {
                float verticalSpeed = character.getVerticalSpeed;
                bool jumpInput = character.isJumpInput;
                if (playerInput.jumpInput <= 0f && character.verticalVelocity.y > 0.0f) // jumpAbortSpeed messes with arc of jump
                    verticalSpeed = Mathf.MoveTowards(verticalSpeed, -character.gravitySettings.maxFallSpeed, character.movement.jumpAbortSpeed * deltaTime);
                else if (character.surfaceCollisions.justWalkedOffEdge && verticalSpeed <= 0)
                {
                    if (jumpInput)
                        verticalSpeed = character.movement.jumpSpeed;
                }
                return Mathf.MoveTowards(verticalSpeed, -character.gravitySettings.maxFallSpeed, character.gravitySettings.gravity * deltaTime);
            };
            character.SetHorizontalSpeed = (deltaTime) =>
            {
                float horizontalSpeed = character.getHorizontalSpeed;
                return Mathf.MoveTowards(horizontalSpeed, 0, deltaTime);
            };


        }
        private void OnSurface() // extra substates can get the substate 
        {

            // if proper connection switch current state to connecting 
            // need some logic for letting player stick to wall
            movementInputWeight = 0;
            character.SetHorizontalSpeed = (deltaTime) =>
            {

                Vector3 movementInput = character.GetMovementInput();
                float horizontalSpeed = character.getHorizontalSpeed;
                if (movementInput.sqrMagnitude > 1.0f)
                {
                    movementInput.Normalize();
                } 
                character.movement.targetHorizontalSpeed = movementInput.magnitude * character.movement.maxHorizontalSpeed;
                return Mathf.MoveTowards(horizontalSpeed, character.movement.targetHorizontalSpeed, acceleration * deltaTime); 

            };
            character.SetVerticalSpeed = (deltaTime) =>
            {
                float verticalSpeed = character.getVerticalSpeed;
                Vector3 normal = character.surfaceCollisions.currentNormal;
                if (normal == Vector3.zero)
                {
                    verticalSpeed = Mathf.MoveTowards(verticalSpeed, -character.gravitySettings.maxFallSpeed, character.movement.jumpAbortSpeed * deltaTime);
                    return Mathf.MoveTowards(verticalSpeed, -character.gravitySettings.maxFallSpeed, character.gravitySettings.gravity * deltaTime);
                }
                // change move amount to running into wall
                float targetVertSpeed = playerInput.moveAmount * character.movement.maxHorizontalSpeed;
                verticalSpeed = verticalSpeed < 0 ? verticalSpeed * .1f : verticalSpeed; 
                return Mathf.MoveTowards(verticalSpeed, targetVertSpeed, 8f * deltaTime) * crouchWeight;
            };
        }



       // Movement

        private void Running()
        {
            if (!character.movement.substates.lockState)
                launcher.Launch(character.movement.substates.HoldCurrentStateTill(() => playerInput.sprintInput <= 0f || playerInput.crouchInput > 0f));
            character.movement.maxHorizontalSpeed = 16f;
        }

        private void Crouching()
        {
            if (!character.movement.substates.lockState)
               launcher.Launch(character.movement.substates.HoldCurrentStateTill(() => playerInput.crouchInput <= 0f || playerInput.sprintInput > 0f));
            character.movement.maxHorizontalSpeed = 4;
            crouchWeight = 0;
        }

        // Void
        private void Void() { }
        #endregion
    }



}

