import cv2
import numpy as np
from pyzbar.pyzbar import decode
import socket
import time
import json
import os

# --- 1. UDP TRANSMISSION CONFIGURATION ---
UDP_IP = "localhost"
UDP_PORT = 5005
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# Unity sends CONFIRM_PLACEMENT here when the Arduino confirm button is pressed.
TRIGGER_PORT = 5006
trigger_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
trigger_sock.bind(("127.0.0.1", TRIGGER_PORT))
trigger_sock.setblocking(False)

# =========================================================================
# 2. ARDUINO CONFIGURATION SECTION
# =========================================================================
# Keep this False when Unity is already connected to Arduino, because one COM
# port cannot be opened by Unity and Python at the same time.
USE_ARDUINO = False
ARDUINO_PORT = 'COM3'
BAUD_RATE = 9600
ser = None

if USE_ARDUINO:
    try:
        import serial
        ser = serial.Serial(ARDUINO_PORT, BAUD_RATE, timeout=0.1)
        print(f"ARDUINO: Successfully connected to port {ARDUINO_PORT}")
    except Exception as e:
        print(f"ARDUINO WARNING: Connection failed ({e}). Switching to Unity trigger mode.")
        ser = None
else:
    print("ARDUINO MODE: DISABLED in Python. Waiting for Unity confirm trigger.")
# =========================================================================

# --- 3. MANAGING 11 SCANNING REGIONS (ROI) ---
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG_FILE = os.path.join(SCRIPT_DIR, "roi_config.json")

def generate_default_rois():
    default_rois = []
    for i in range(6):
        default_rois.append([40 + i * 100, 100, 80, 80])
    for i in range(5):
        default_rois.append([90 + i * 100, 250, 80, 80])
    return default_rois

if os.path.exists(CONFIG_FILE):
    with open(CONFIG_FILE, "r") as f:
        ROI_ZONES = json.load(f)
    print("CONFIG: Successfully loaded coordinates for 11 scanning zones!")
else:
    ROI_ZONES = generate_default_rois()
    print("CONFIG: Created default 11 scanning zones.")

show_boxes = True
current_selected = 0

# --- 4. SYNCHRONIZED QR SCANNING FUNCTION FOR ALL 11 ZONES ---
def decode_qr_from_roi(color_roi):
    if color_roi.size == 0:
        return None

    gray_roi = cv2.cvtColor(color_roi, cv2.COLOR_BGR2GRAY)
    scan_candidates = [color_roi, gray_roi]

    for scale in (2, 3, 4):
        enlarged = cv2.resize(gray_roi, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
        scan_candidates.append(enlarged)

        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        contrast = clahe.apply(enlarged)
        scan_candidates.append(contrast)

        thresh = cv2.adaptiveThreshold(
            contrast, 255,
            cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
            cv2.THRESH_BINARY, 21, 5
        )
        scan_candidates.append(thresh)

    for candidate in scan_candidates:
        detected = decode(candidate)
        if len(detected) > 0:
            return detected[0].data.decode('utf-8').strip()

    return None


def scan_all_zones(current_frame):
    """Crop 11 regions, sharpen and upscale images to optimize long-distance QR scanning."""
    zones_result = ["None"] * 11

    h_img, w_img = current_frame.shape[:2]

    for idx, (x, y, w, h) in enumerate(ROI_ZONES):
        x_start, y_start = max(0, x), max(0, y)
        x_end, y_end = min(w_img, x + w), min(h_img, y + h)
        color_roi = current_frame[y_start:y_end, x_start:x_end]
        obj_type = decode_qr_from_roi(color_roi)

        if obj_type:
            zones_result[idx] = obj_type

    return zones_result


def send_scan_results(current_frame, source_label):
    print(f"{source_label} Scanning all 11 zones on the map...")
    final_results = scan_all_zones(current_frame)
    packet_json = json.dumps({"zones": final_results})
    sock.sendto(packet_json.encode('utf-8'), (UDP_IP, UDP_PORT))
    print(f" -> Sent JSON to Unity: {packet_json}")


# --- 5. CAMERA INITIALIZATION ---
cap = cv2.VideoCapture(1, cv2.CAP_DSHOW)

if not cap.isOpened():
    print("Camera index 1 failed. Trying camera index 0...")
    cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)

print("\n" + "=" * 50)
print("=== PYTHON: 11-ZONE MAP SCANNER SYSTEM ACTIVATED ===")
print("=" * 50)
print("SCANNING INSTRUCTIONS:")
print("  - [Confirm Button] : CAPTURE AND SCAN ALL 11 ZONES (Send to Unity)")
print("  - [C Key]          : Keyboard fallback for testing")
print("  - [Q]              : Exit program")
print("=" * 50 + "\n")

while True:
    ret, frame = cap.read()
    if not ret:
        print("Error: Cannot read video stream.")
        break

    display_frame = frame.copy()
    preview_results = scan_all_zones(frame)

    if show_boxes:
        for idx, (x, y, w, h) in enumerate(ROI_ZONES):
            detected_text = preview_results[idx]

            if detected_text != "None":
                color = (255, 0, 255)
                thickness = 3
            elif idx == current_selected:
                color = (0, 0, 255)
                thickness = 3
            else:
                color = (0, 255, 0)
                thickness = 1

            cv2.rectangle(display_frame, (x, y), (x + w, y + h), color, thickness)
            cv2.putText(display_frame, f"Z_{idx + 1}", (x + 3, y + 15),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.4, color, 1)

            if detected_text != "None":
                cv2.putText(display_frame, detected_text, (x + 3, y + h - 6),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.45, color, 1)

        cv2.putText(display_frame, f"Editing Zone: {current_selected + 1}",
                    (15, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)

    cv2.imshow('Webcam Scanner', display_frame)

    if ser and ser.in_waiting > 0:
        try:
            incoming_data = ser.readline().decode('utf-8').strip()
            if incoming_data == "TRIGGER" or incoming_data == "CONFIRM_PLACEMENT":
                send_scan_results(frame, "[ARDUINO TRIGGER]")
        except Exception:
            pass

    try:
        trigger_data, _ = trigger_sock.recvfrom(1024)
        trigger_message = trigger_data.decode("utf-8").strip()
        if trigger_message == "CONFIRM_PLACEMENT":
            send_scan_results(frame, "[CONFIRM BUTTON]")
    except BlockingIOError:
        pass

    key = cv2.waitKey(1) & 0xFF

    if key == ord('q') or key == ord('Q'):
        break

    elif key == ord('c') or key == ord('C'):
        send_scan_results(frame, "[KEYBOARD C]")

    elif key == ord('v') or key == ord('V'):
        show_boxes = not show_boxes

    elif key == ord('S'):
        with open(CONFIG_FILE, "w") as f:
            json.dump(ROI_ZONES, f)
        print("Saved coordinates for 11 scanning zones!")

    elif ord('0') <= key <= ord('9'):
        current_selected = key - ord('0')

    elif key == ord('-'):
        current_selected = 10

    elif key == ord('w') or key == ord('W'):
        ROI_ZONES[current_selected][1] -= 2

    elif key == ord('s'):
        ROI_ZONES[current_selected][1] += 2

    elif key == ord('a') or key == ord('A'):
        ROI_ZONES[current_selected][0] -= 2

    elif key == ord('d') or key == ord('D'):
        ROI_ZONES[current_selected][0] += 2

    elif key == ord('i') or key == ord('I'):
        ROI_ZONES[current_selected][3] -= 2

    elif key == ord('k') or key == ord('K'):
        ROI_ZONES[current_selected][3] += 2

    elif key == ord('j') or key == ord('J'):
        ROI_ZONES[current_selected][2] -= 2

    elif key == ord('l') or key == ord('L'):
        ROI_ZONES[current_selected][2] += 2

cap.release()
cv2.destroyAllWindows()

if ser:
    ser.close()

trigger_sock.close()
print("=== PROGRAM CLOSED ===")
