#include "ServerUDP.h"

using namespace Networking;

/*
	Initialize socket, server address to lookup to, and connect to the server

	@return: socket file descriptor
*/
int ServerUDP::InitializeSocket(short port)
{
  int err = -1;

    /* Create a TCP streaming socket */
    if ((_UDPReceivingSocket = socket(AF_INET, SOCK_DGRAM, 0)) == -1 )
    {
        fatal("InitializeSocket: socket() failed\n");
        return _UDPReceivingSocket;
    }

    /* Fill in server address information */
    memset(&_ServerAddress, 0, sizeof(struct sockaddr_in));
    _ServerAddress.sin_family = AF_INET;
    _ServerAddress.sin_port = htons(port);

    /* bind server address to accepting socket */
    if ((err = bind(_UDPReceivingSocket, (struct sockaddr *)&_ServerAddress, sizeof(_ServerAddress))) == -1)
    {
        std::cout << "InitializeSocket: bind() failed with errno " << errno << std::endl;
        return err;
    }
    return 0;
}

void * ServerUDP::Receive()
{
    int err;
    struct sockaddr_in Client;              /* Incoming client's socket address information */
    unsigned ClientLen = sizeof(Client);
    char* buf = (char *)malloc(BUFSIZE);

    while (1)
    {
      if((err = recvfrom(_UDPReceivingSocket, buf, PACKETLEN, 0, (sockaddr *)&Client, &ClientLen)) <= 0)
      {
          fatal("UDP_Server_Recv: recvfrom() failed\n");
          return 0;
      }
      std::cout << buf << std::endl;
      //this->ServerUDP::Broadcast(buf);
    }
    free(buf);
}

/*
	Sends a message to all the clients
*/
void ServerUDP::Broadcast(char* message)
{
  for(std::vector<int>::size_type i = 0; i != _PlayerList.size(); i++)
  {
    if(sendto(_UDPReceivingSocket, message, PACKETLEN, 0, (sockaddr *)&_PlayerList[i].connection, sizeof(&_PlayerList[i].connection)) == -1)
    {
      std::cerr << "errno: " << errno << std::endl;
      return;
    }
  }
}
/*
  Registers the passed in Player list as a class member to be used in broadcasting UDP packets.
*/
void ServerUDP::SetPlayerList(std::vector<Player> players)
{
  _PlayerList = players;
}
