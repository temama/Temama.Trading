import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { UserService } from '../login/UserService';

@Component({
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.css']
})
export class NavMenuComponent implements OnInit {

  username = '';
  isExpanded = false;

  constructor(private router: Router, private user: UserService) { }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }

  ngOnInit() {
    this.user.loginStateChanged.subscribe(() => this.checkLogin());
    this.checkLogin();
  }

  checkLogin() {
    this.username = this.user.name();
    if (!this.user.isLoggedIn()) {
      this.router.navigateByUrl('/login');
    }
  }

  logout() {
    this.user.logout();
    this.router.navigateByUrl('/login');
  }
}
