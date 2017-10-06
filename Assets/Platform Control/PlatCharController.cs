using UnityEngine;
using System.Collections;

public class PlatCharController : MonoBehaviour {

	// Horizontal speed
	public float maxSpeed = 8f;

	// What we sent the Y velocity to during a jump
	public float jumpSpeed = 12f;

	// Whether our platform will add to jump
	public bool platformRelativeJump    = false;

	// Pushing towards a wall will grab on to it.
	public bool allowWallGrab = true;
	
	// Pushing towards a wall will grab on to it.
	public bool allowWallJump = true;
	
	// If this is false, we are likely to slide down walls while grabbing (depending on
	// gravity and/or surface friction).  Note that the current circle-based colliders
	// result in weird drifting if you enable this.
	public bool disableGravityDuringWallGrab = false;

	// What layers are valid for us to grab?  For example, we probably can't
	// grab enemies, icy surfaces, etc...  Most walls/floors/platforms
	// should be in the "grabbable" layer(s).
	public LayerMask wallGrabMask;
	
	// After we wall jump, ignore left/right input for a moment.
	public float wallJumpControlDelay = 0.15f;
	float wallJumpControlDelayLeft = 0;

	// Relative to our transform/pivot point, where are we testing for grabbing?
	// Logically, this should probably be around where the character's hand will
	// be during the grabbing animation.
	public Vector2 grabPoint = new Vector2(0.45f, 0f);

	// Bookkeeping Variables
	MovingPlatform movingPlatform;		// The moving platform we are touching
	Animator anim;						// Our animator
	bool groundedLastFrame = false;		// Were we grounded last frame? Used by IsGrounded to eliminate top-of-arc issues.
	bool jumping = false;				// Is the player commanding us to jump?

	// Use this for initialization
	void Start () {
		anim = GetComponent<Animator>();
        Physics2D.IgnoreCollision(this.GetComponent<BoxCollider2D>(), GameObject.FindGameObjectWithTag("Enemy").GetComponent<BoxCollider2D>(), true);
	}

	void OnCollisionEnter2D(Collision2D col) {
		// If we collided against a platform, grab a copy of it and we can use it as our zero point for IsGrounded.
		MovingPlatform mp = col.transform.root.GetComponent<MovingPlatform>();
		if(mp != null) {
			Debug.Log ("movingPlatform: " + mp.gameObject.name);
			movingPlatform = mp;
		}
	}

	/// <summary>
	/// Determines if the character is grounded based on having a zero velocity relative to his platform.
	/// </summary>
	bool IsGrounded() {
		if(Mathf.Abs ( RelativeVelocity().y ) < 0.1f) {	// Checking floats for exact equality is bad. Also good for platforms that are moving down.

			// Since it's possible for our velocity to be exactly zero at the apex of our jump,
			// we actually require two zero velocity frames in a row.

			if(groundedLastFrame)
				return true;

			groundedLastFrame = true;
		}
		else {
			groundedLastFrame = false;
		}

		return false;
	}

	/// <summary>
	/// Determines if we're grabbing a surface.
	/// </summary>
	bool IsGrabbing() {
		if(allowWallGrab == false)
			return false;

		// FIXME: Is there any chance we want to set movingPlatform here?

		// If we're pushing the joystick in the direction of our facing and an OverlapCircle test indicates a grabbable surface at the grabPoint, return true.
		return ((Input.GetAxisRaw("Horizontal") > 0 && this.transform.localScale.x > 0) || (Input.GetAxisRaw("Horizontal") < 0 && this.transform.localScale.x < 0)) &&
			Physics2D.OverlapCircle(this.transform.position + new Vector3( grabPoint.x * this.transform.localScale.x, grabPoint.y, 0), 0.2f, wallGrabMask);
	}
		 
	void Update() {
		// Get____Down and Get____Up are only reliable inside of Update(), not FixedUpdate().
		if( Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump") )
			jumping = true;

	}

	/// <summary>
	/// Our velocity relative to the platform we're on, if any.
	/// </summary>
	/// <returns>The velocity.</returns>
	Vector2 RelativeVelocity() {
		return GetComponent<Rigidbody2D>().velocity - PlatformVelocity();
	}

	/// <summary>
	/// The velocity of the platform we're on (or zero)
	/// </summary>
	/// <returns>The velocity.</returns>
	Vector2 PlatformVelocity() {
		if(movingPlatform==null)
			return Vector2.zero;
		
		return movingPlatform.GetComponent<Rigidbody2D>().velocity;
	}

	// Update is called once per physics loop
	void FixedUpdate () {
		bool isGrounded = IsGrounded();
		bool isGrabbing = !isGrounded && wallJumpControlDelayLeft <= 0 && IsGrabbing();

		if(movingPlatform != null && !groundedLastFrame && !isGrabbing && !isGrounded) {
			// We aren't grounded or grabbing.  Making sure to clear our platform.
			movingPlatform = null;
		}

		// FIXME: This results in weird drifting with our current colliders
		if(disableGravityDuringWallGrab) {
			if(isGrabbing)
				GetComponent<Rigidbody2D>().gravityScale = 0;
			else 
				GetComponent<Rigidbody2D>().gravityScale = 1;
		}

		// We start off by assuming we are maintaining our velocity.
		float xVel = GetComponent<Rigidbody2D>().velocity.x;
		float yVel = GetComponent<Rigidbody2D>().velocity.y;

		// If we're grounded, maintain our velocity at platform velocity, with slight downward pressure to maintain the collision.
		if(isGrounded) {
			yVel = PlatformVelocity().y - 0.01f;
		}

		// Some moves (like walljumping) might introduce a delay before x-velocity is controllable
		wallJumpControlDelayLeft -= Time.deltaTime;

		if(isGrounded || isGrabbing) {
			wallJumpControlDelayLeft = 0;	// Clear the delay if we're in contact with the ground/wall
		}

		// Allow x-velocity control
		if(wallJumpControlDelayLeft <= 0) {
			xVel = Input.GetAxis("Horizontal") * maxSpeed;
			xVel += PlatformVelocity().x;
		}

		if(isGrabbing && RelativeVelocity().y <= 0) {
			// NOTE:  Depending on friction and gravity, the character
			// will still be sliding down unless we turn off gravityScale
			yVel = PlatformVelocity().y;

			// If we are are zero velocity (or "negative" relative to the facing of the wall)
			// set our velocity to zero so we don't bounce off
			// Also ensures that we don't inter-penetrate for one frame, which could cause us
			// to get stuck on a "ledge" between blocks.
			if(RelativeVelocity().x * transform.localScale.x <= 0) {
				xVel = PlatformVelocity().x;
			}
		}

		if(jumping && (isGrounded || (isGrabbing && allowWallJump) )) {
			// NOTE: As-is, neither vertical velocity nor walljump speed is affected by PlatformVelocity().
			yVel = jumpSpeed;
			if(platformRelativeJump)
				yVel += PlatformVelocity().y;

			if(isGrabbing) {
				xVel = -maxSpeed * this.transform.localScale.x;
				wallJumpControlDelayLeft = wallJumpControlDelay;
			}
		}
		jumping = false;


		// Apply the calculate velocity to our rigidbody
		GetComponent<Rigidbody2D>().velocity = new Vector2(
				xVel,
				yVel
			);

		// Update facing
		Vector3 scale = this.transform.localScale;
		if(scale.x < 0 && Input.GetAxis("Horizontal") > 0) {
			scale.x = 1;
		}
		else if(scale.x > 0 && Input.GetAxis("Horizontal") < 0) {
			scale.x = -1;
		}
		this.transform.localScale = scale;

		// Update animations
		anim.SetFloat("xSpeed", Mathf.Abs(RelativeVelocity().x));

		if(isGrabbing)
			anim.SetFloat("ySpeed", Mathf.Abs(1000));
		else
			anim.SetFloat("ySpeed", RelativeVelocity().y);
	}

    public void OnTriggerEnter2D(Collider2D c)
    {
        if(c.tag == "Enemy")
        {
            Debug.Log("Trigger Hit!");
            if (gameObject.transform.position.x < GameObject.FindWithTag("Enemy").transform.position.x)
            {
                GetComponent<Rigidbody2D>().AddForce((Vector2.left * 6000), ForceMode2D.Force);
            }
            else{
                GetComponent<Rigidbody2D>().AddForce((Vector2.right * 6000), ForceMode2D.Force);
            }
        }
    }
}
