import socket
import time
import struct

SERVER_IP = '127.0.0.1'
SERVER_PORT = 10809

def send_message_with_length(sock, message):
    # Encode the message with UTF-8 and prepend its length
    message_bytes = message.encode('utf-8')
    # length_prefix = struct.pack('<I', len(message_bytes))
    # sock.sendall(length_prefix + message_bytes)

    message_bytes = message.encode('utf-8')
    sock.sendall(message_bytes)

def receive_message_with_length(sock):
    # # Receive the 4-byte length prefix
    # length_prefix = sock.recv(4)
    # if not length_prefix:
    #     return None
    # message_length = struct.unpack('<I', length_prefix)[0]
    # # Loop to receive the full message content
    # chunks = []
    # bytes_recd = 0
    # while bytes_recd < message_length:
    #     chunk = sock.recv(min(message_length - bytes_recd, 2048))
    #     if not chunk:
    #         break
    #     chunks.append(chunk)
    #     bytes_recd += len(chunk)
    # message_bytes = b''.join(chunks)
    # return message_bytes.decode('utf-8')
    
    # 直接接收一个缓冲区的数据
    message_bytes = sock.recv(2048)
    if not message_bytes:
        return None
    return message_bytes.decode('utf-8')

def convert_value(item):
    s = item.strip()
    # Attempt to convert to boolean
    if s.lower() == "true":
        return True
    if s.lower() == "false":
        return False

    # Attempt to convert to a 3D vector (a, b, c)
    if s.startswith("(") and s.endswith(")"):
        inner = s[1:-1]
        parts = inner.split(',')
        if len(parts) == 3:
            try:
                return (float(parts[0].strip()), float(parts[1].strip()), float(parts[2].strip()))
            except ValueError:
                pass

    # Attempt to convert to a floating point number
    try:
        return float(s)
    except ValueError:
        pass

    # Return the original string if no conversion applies
    return s

def parse_message_to_list(message):
    # Split the message by "<<" delimiter, trim and convert the type of each item
    items = message.split("<<")
    return [convert_value(item) for item in items if item.strip()]

def demo_client():
    try:
        client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client_socket.connect((SERVER_IP, SERVER_PORT))
        print(f"Connected to server {SERVER_IP}:{SERVER_PORT}")

        while True:
            # Construct a demo message including: string, boolean, float, and 3D vector
            demo_message = "HelloWorld<<true<<123.456<<(1.0, 2.0, 3.0)"
            print(f"Sending message: {demo_message}")
            send_message_with_length(client_socket, demo_message)

            response = receive_message_with_length(client_socket)
            if response is None:
                print("No response from server, closing connection.")
                break

            print(f"Received echo from server: {response}")
            result_list = parse_message_to_list(response)
            print("Parsed variable types:")
            for index, item in enumerate(result_list):
                print(f"Item {index + 1}: {item} ({type(item).__name__})")
            
            # Wait for 1 second before next iteration
            # time.sleep(1)

    except Exception as e:
        print(f"Error occurred: {e}")
    finally:
        client_socket.close()

if __name__ == "__main__":
    demo_client()