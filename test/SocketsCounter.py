import socket
import time
import threading

SERVER_IP = '127.0.0.1'
SERVER_PORT = 10809

class SocketCounter:
    def __init__(self):
        self.receive_count = 0
        self.last_count = 0
        self.running = True
        
    def send_message(self, sock, message):
        message_bytes = message.encode('utf-8')
        sock.sendall(message_bytes)

    def receive_message(self, sock):
        message_bytes = sock.recv(2048)
        if not message_bytes:
            return None
        self.receive_count += 1
        return message_bytes.decode('utf-8')

    def print_rate(self):
        while self.running:
            time.sleep(1)
            current_rate = self.receive_count - self.last_count
            self.last_count = self.receive_count
            print(f"\r每秒接收次数: {current_rate}", end="", flush=True)

    def run_client(self):
        # 启动统计线程
        rate_thread = threading.Thread(target=self.print_rate)
        rate_thread.daemon = True
        rate_thread.start()
        
        try:
            client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            client_socket.connect((SERVER_IP, SERVER_PORT))

            while True:
                demo_message = "HelloWorld<<true<<123.456<<(1.0, 2.0, 3.0)"
                self.send_message(client_socket, demo_message)
                
                response = self.receive_message(client_socket)
                if response is None:
                    break

        except Exception as e:
            print(f"\n错误: {e}")
        finally:
            self.running = False
            client_socket.close()

if __name__ == "__main__":
    counter = SocketCounter()
    counter.run_client()
