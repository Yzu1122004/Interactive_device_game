import cv2
print(cv2.__version__)
from pyzbar.pyzbar import decode
import socket

# 1. Configure UDP network connection to Unity
UDP_IP = "localhost"
UDP_PORT = 5005
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# 2. Initialize Webcam
cap = cv2.VideoCapture(0)

print("=== MULTIPLE QR CODE SCANNER IS RUNNING ===")
print("Please place all blocks in position. The program will auto-exit after scanning.")

while True:
    ret, frame = cap.read()
    if not ret:
        break

    # Scan all QR codes present in the current frame
    detected_qrcodes = decode(frame)

    # If AT LEAST one QR Code is detected
    if len(detected_qrcodes) > 0:
        print(f"-> Found {len(detected_qrcodes)} QR Code(s)!")
        
        for qrcode in detected_qrcodes:
            qr_data = qrcode.data.decode('utf-8')
            print(f"   + Sending data: {qr_data}")
            
            # Send each object data package to Unity via UDP
            sock.sendto(qr_data.encode('utf-8'), (UDP_IP, UDP_PORT))
        
        print("-> All data sent to Unity successfully. Closing camera...")
        break # Exit the loop and terminate the program immediately

    # Display the camera stream while waiting for block placement
    cv2.imshow('Webcam Scanner', frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
print("=== PROGRAM TERMINATED ===")
