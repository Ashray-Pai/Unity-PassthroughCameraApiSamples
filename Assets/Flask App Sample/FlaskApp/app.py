from flask import Flask, request, jsonify
import cv2
import numpy as np
from PIL import Image
import mediapipe as mp

app = Flask(__name__)

# Initialize MediaPipe Pose
mp_pose = mp.solutions.pose
# pose = mp_pose.Pose(static_image_mode=True, min_detection_confidence=0.5)
pose = mp_pose.Pose(static_image_mode=False, min_detection_confidence=0.8, min_tracking_confidence=0.8)


def detect_pose(image: Image.Image):
    """Runs MediaPipe Pose detection on an image and returns keypoints with z."""
    # Convert PIL image to OpenCV format (RGB -> BGR)
    image_cv = np.array(image)
    image_cv = cv2.cvtColor(image_cv, cv2.COLOR_RGB2BGR)
    print(f"Image received: {image_cv.shape}")  # Should print (480, 640, 3)

    # Get original dimensions
    original_height, original_width = image_cv.shape[:2]

    # Process the image with MediaPipe Pose
    results = pose.process(cv2.cvtColor(image_cv, cv2.COLOR_BGR2RGB))

    keypoints = []
    if results.pose_landmarks:
        for landmark in results.pose_landmarks.landmark:
            x = landmark.x * original_width
            y = landmark.y * original_height
            z = landmark.z
            # Flip y-coordinate to match Unity's texture coordinate system (0 at bottom)
            #x = original_width - x

            #y = original_height - y
            keypoints.append((x, y, z))
        print(f"Keypoints detected: {len(keypoints)}")

    return keypoints  # Return only keypoints

@app.route("/detect", methods=["POST"])
def detect_objects():
    """Detects pose keypoints from an uploaded image."""
    if "file" not in request.files:
        return jsonify({"error": "No file uploaded"}), 400

    file = request.files["file"]
    image = Image.open(file.stream).convert("RGB")
    keypoints = detect_pose(image)  # Single return value

    # Convert keypoints to a list of lists for JSON serialization
    keypoints_list = [[float(x), float(y), float(z)] for x, y, z in keypoints]

    return jsonify({"keypoints": keypoints_list})

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)